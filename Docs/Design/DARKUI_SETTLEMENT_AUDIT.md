# DarkUI Free Settlement audit

Date: 2026-09-28. Source: current local working tree, including the pre-existing Settlement scene and B.png importer edits. No commit or push.

## Source and import decisions

`Assets/Dark UI/Free/` contains **65 PNG images**. All were inspected in a contact sheet with dimensions; representative panel/button alpha edges were measured directly. Original image bytes and asset GUIDs are preserved. `Icon Sets.png` is a single plus icon, not an atlas.

The source is smooth, antialiased white artwork with transparent backgrounds, not pixel-art UI. Every image uses Sprite / Single, Full Rect mesh, 100 PPU, Bilinear, Clamp, sRGB, input alpha with Alpha Is Transparency, no mipmaps, no NPOT rescale, and uncompressed texture data. No source pixels or vendor scripts changed. Existing platform overrides are disabled; the lossless default applies.

Category counts: Circular frame/icon container: 6, Divider/header line: 1, Icon: 30, Input/dropdown outline (fixed aspect): 1, Panel/card fill: 3, Panel/frame outline: 6, Pill button (fixed aspect): 9, Rounded button/card fill: 3, Rounded button/tab frame: 6.

No separate hover/pressed/selected art, dedicated slider/scrollbar set, checkbox marks, modal dimmer, or decorative header/corner set exists. These roles use the existing controls and semantic tints. The dimmers remain their existing uniform Images; no decorative transparency is stretched over a modal blocker.

Nine panel fills/outlines and nine rounded button fills/outlines have measured slice borders. The 32-pixel-radius sources reach straight edges by 32 pixels; the cuts include the antialias/stroke fringe (34 for fills/thin frames, 35 for A/B/C, 36 for four-pixel panel outlines). The 64/128-radius variants follow the same measured geometry. B retains the locally configured 35-pixel border. All opposing cuts leave a nonempty center.

Circles, pill outlines and pictograms remain unsliced and available with their native aspect ratio. The flat Divider uses Simple with horizontal scaling; it has no decorative caps to protect. No arbitrary nine-slicing is applied to icons or circles.

## Authored visual vocabulary

| Role | Source | Source border L/B/R/T | Image type | PPU multiplier |
|---|---|---|---|---|
| Main, secondary, modal, detail and scroll-well panels | `32.png` | 34/34/34/34 | Sliced | 8 |
| Existing major panel/preview frames | `32 Light.png` | 36/36/36/36 | Sliced | 4 |
| Navigation, ordinary actions, equipment and reinforcement cards, archive rows, dropdowns | `Button_Small_A.png` | 34/34/34/34 | Sliced | 8 |
| Resource cell backgrounds | `Button_Small_A.png` | 34/34/34/34 | Sliced | 16 |
| Equipment branch state frames | `B.png` | 35/35/35/35 | Sliced | 6 |
| Slider/scrollbar tracks and fills | `32.png` | 34/34/34/34 | Sliced | 32 |
| Handles, toggle backgrounds and existing geometric marks | `32.png` | 34/34/34/34 | Sliced | 16 |
| Horizontal rules | `Divider.png` | 0 | Simple, stretch length | n/a |
| Directional controls/dropdown arrows | `Left Arrow.png`, `Right Arrow.png`, `Down Arrow.png` | 0 | Simple, preserve aspect | n/a |
| Retained inactive legacy Settings Close button | `Button_Large_A.png` | 34/34/34/34 | Sliced | 1, existing |
| Retained inactive trait-template lock container | `CIRCLE4PXSMA.png` | 0 | Simple | 1, existing |

All sources use Bilinear and 100 PPU. Multipliers control corner sizes locally at the existing Canvas reference PPU; source import scale is not changed to suit one screen. The neutral fills allow yellow selection, cyan/blue hover/focus, readable disabled states, purple abnormal-technology accents and existing orange/green/blue campaign identities to remain authoritative. StatPresentation is unchanged. The three research Special items keep their established campaign identity accents.

The saved hierarchy, navigation, callbacks, templates, resource ordering/zero-compaction and owner references remain intact. Reinforcement cards retain their captured dark Image baseline. Equipment labels no longer double-reserve the icon column; their font size remains 7.5 with local width adjustment capped at 20%. The Recovery result label gains vertical space. Recovery condition text replaces the unsupported checkmark with ASCII [OK] and uses - for unmet conditions; only the marker changed.

## Complete image inventory

Paths in this table are relative to `Assets/Dark UI/Free/`. Import profile **UI** means the common settings above. A border shown as a single number applies equally to all four sides. “Used” means an authored Settlement Canvas reference, including existing inactive controls and templates.

| Image | Dimensions | Profile | Border | Intended role | Settlement use / reason retained unused |
|---|---:|---|---:|---|---|
| `128 Light 2.png` | 514x515 | UI | 130 | Panel/frame outline | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `128 Light.png` | 516x516 | UI | 132 | Panel/frame outline | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `128.png` | 512x513 | UI | 130 | Panel/card fill | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `32 Light 2.png` | 514x515 | UI | 34 | Panel/frame outline | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `32 Light.png` | 516x516 | UI | 36 | Panel/frame outline | Used: 15 authored Images; exact paths below |
| `32.png` | 512x513 | UI | 34 | Panel/card fill | Used: 74 authored Images; exact paths below |
| `64 Light 2.png` | 514x515 | UI | 66 | Panel/frame outline | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `64 Light.png` | 516x516 | UI | 68 | Panel/frame outline | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `64.png` | 512x513 | UI | 66 | Panel/card fill | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `A.png` | 516x133 | UI | 35 | Rounded button/tab frame | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `Add Icon.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `B.png` | 390x133 | UI | 35 | Rounded button/tab frame | Used: 9 authored Images; exact paths below |
| `Bell.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `BTN_A1.png` | 258x131 | UI | 34 | Rounded button/tab frame | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `BTN_A2.png` | 388x131 | UI | 34 | Rounded button/tab frame | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `BTN_A3.png` | 514x131 | UI | 34 | Rounded button/tab frame | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `BTN_Line_4PX_Large.png` | 516x133 | UI | 0 | Pill button (fixed aspect) | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `BTN_Line_4PX_Medium.png` | 390x133 | UI | 0 | Pill button (fixed aspect) | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `BTN_Line_4PX_Small.png` | 260x133 | UI | 0 | Pill button (fixed aspect) | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `Button_Large_A.png` | 512x129 | UI | 34 | Rounded button/card fill | Used: 1 authored Images; exact paths below |
| `Button_Medium_A.png` | 386x129 | UI | 34 | Rounded button/card fill | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `Button_Small_A.png` | 256x129 | UI | 34 | Rounded button/card fill | Used: 128 authored Images; exact paths below |
| `ButtonLargeRound.png` | 512x129 | UI | 0 | Pill button (fixed aspect) | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `ButtonMediumRound.png` | 386x129 | UI | 0 | Pill button (fixed aspect) | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `ButtonSmallRound.png` | 256x129 | UI | 0 | Pill button (fixed aspect) | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `C.png` | 260x133 | UI | 35 | Rounded button/tab frame | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `CIRCLE2PXLAR.png` | 202x203 | UI | 0 | Circular frame/icon container | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `CIRCLE2PXMED.png` | 152x153 | UI | 0 | Circular frame/icon container | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `CIRCLE2PXSMALL.png` | 130x131 | UI | 0 | Circular frame/icon container | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `CIRCLE4PXLAR.png` | 204x205 | UI | 0 | Circular frame/icon container | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `CIRCLE4PXMED.png` | 154x155 | UI | 0 | Circular frame/icon container | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `CIRCLE4PXSMA.png` | 132x133 | UI | 0 | Circular frame/icon container | Used: 1 authored Images; exact paths below |
| `Close BTN.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Coin.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `D.png` | 514x131 | UI | 0 | Pill button (fixed aspect) | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `Divider.png` | 256x9 | UI | 0 | Divider/header line | Used: 3 authored Images; exact paths below |
| `Down Arrow.png` | 128x128 | UI | 0 | Icon | Used: 3 authored Images; exact paths below |
| `Down BTN.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `E.png` | 388x131 | UI | 0 | Pill button (fixed aspect) | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `F.png` | 258x131 | UI | 0 | Pill button (fixed aspect) | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `Facebook 2.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Facebook.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Gem.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Heart.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Icon Sets.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Info.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `InputField.png` | 512x128 | UI | 0 | Input/dropdown outline (fixed aspect) | Unused: alternate geometry; the smaller role set keeps Settlement consistent |
| `Inventory.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Left Arrow.png` | 128x128 | UI | 0 | Icon | Used: 2 authored Images; exact paths below |
| `Left BTN.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Lock.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Messages.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Missions.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Music.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Ranks.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Right Arrow.png` | 128x128 | UI | 0 | Icon | Used: 2 authored Images; exact paths below |
| `Right BTN.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Settings.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Shop.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Sound.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Star.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Up Arrow.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `Up BTN.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `World.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |
| `X.png` | 128x128 | UI | 0 | Icon | Unused: retain existing resource/story/gameplay icons and navigation identity bars |

**10 source images referenced; 55 configured and intentionally unused.** No full-pack duplication.

## Exact authored references

Every path below is in `Assets/01_Scenes/Settlement.unity`. No production prefab was changed by this pass. Existing legacy/inactive references remain authored; this pass does not reactivate retired screens.

### `Assets/Dark UI/Free/32.png`

| Hierarchy path | Image type | PPU multiplier |
|---|---|---:|
| `Canvas/EscSettingsRoot/SettingPanel` | Sliced | 4 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayConfirmation/ConfirmationPanel` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FrameLimitDropdown/Template` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FrameLimitDropdown/Template/Scrollbar` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FrameLimitDropdown/Template/Scrollbar/Sliding Area/Handle` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FrameLimitDropdown/Template/Viewport/Content/Item/Item Background` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FrameLimitDropdown/Template/Viewport/Content/Item/Item Checkmark` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FullscreenDropdown/Template` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FullscreenDropdown/Template/Scrollbar` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FullscreenDropdown/Template/Scrollbar/Sliding Area/Handle` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FullscreenDropdown/Template/Viewport/Content/Item/Item Background` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FullscreenDropdown/Template/Viewport/Content/Item/Item Checkmark` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/ResolutionDropdown/Template` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/ResolutionDropdown/Template/Scrollbar` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/ResolutionDropdown/Template/Scrollbar/Sliding Area/Handle` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/ResolutionDropdown/Template/Viewport/Content/Item/Item Background` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/ResolutionDropdown/Template/Viewport/Content/Item/Item Checkmark` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/VSyncToggle` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/VSyncToggle/Checkmark` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/GameplayTab/CameraShakeSlider/Background` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/GameplayTab/CameraShakeSlider/Fill Area/Fill` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/GameplayTab/CameraShakeSlider/Handle Slide Area/Handle` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/GameplayTab/ShowHintsToggle` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/GameplayTab/ShowHintsToggle/Checkmark` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/GameplayTab/WarningOpacitySlider/Background` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/GameplayTab/WarningOpacitySlider/Fill Area/Fill` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/GameplayTab/WarningOpacitySlider/Handle Slide Area/Handle` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/AmbientSlider/Background` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/AmbientSlider/Fill Area/Fill` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/AmbientSlider/Handle Slide Area/Handle` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/BgmSlider/Background` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/BgmSlider/Fill Area/Fill` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/BgmSlider/Handle Slide Area/Handle` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/MasterSlider/Background` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/MasterSlider/Fill Area/Fill` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/MasterSlider/Handle Slide Area/Handle` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/SfxSlider/Background` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/SfxSlider/Fill Area/Fill` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/SfxSlider/Handle Slide Area/Handle` | Sliced | 16 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/UiSlider/Background` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/UiSlider/Fill Area/Fill` | Sliced | 32 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTab/UiSlider/Handle Slide Area/Handle` | Sliced | 16 |
| `Canvas/resourcesUI` | Sliced | 3 |
| `Canvas/SettlementHUD/CampaignProgressPanel` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/Transcript` | Sliced | 8 |
| `Canvas/SettlementHUD/MainPanel/Player_Select` | Sliced | 8 |
| `Canvas/SettlementHUD/NavigationBackground` | Sliced | 8 |
| `Canvas/SettlementHUD/Repair_HUD/Bg` | Sliced | 8 |
| `Canvas/SettlementHUD/Repair_HUD/Cost/LeftPanel` | Sliced | 4 |
| `Canvas/SettlementHUD/Repair_HUD/line` | Sliced | 8 |
| `Canvas/SettlementHUD/SectorTechnologyPanel/ContentBackground` | Sliced | 8 |
| `Canvas/SettlementHUD/SectorTechnologyPanel/SelectedTechnology` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Effects` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Inspection/Growth` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/LeftPanel` | Sliced | 3 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/LeftPanel/Cost/LeftPanel` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Mucin_Panel` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Mucin_Panel/SharedScrollView/Scrollbar Vertical` | Sliced | 32 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Mucin_Panel/SharedScrollView/Scrollbar Vertical/Sliding Area/Handle` | Sliced | 16 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Share_Panel` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Share_Panel/SharedScrollView/Scrollbar Vertical` | Sliced | 32 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Share_Panel/SharedScrollView/Scrollbar Vertical/Sliding Area/Handle` | Sliced | 16 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Shotgun_Panel ` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Shotgun_Panel /SharedScrollView/Scrollbar Vertical` | Sliced | 32 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Shotgun_Panel /SharedScrollView/Scrollbar Vertical/Sliding Area/Handle` | Sliced | 16 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Sniper_Panel ` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Sniper_Panel /SharedScrollView/Scrollbar Vertical` | Sliced | 32 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Sniper_Panel /SharedScrollView/Scrollbar Vertical/Sliding Area/Handle` | Sliced | 16 |

### `Assets/Dark UI/Free/32 Light.png`

| Hierarchy path | Image type | PPU multiplier |
|---|---|---:|
| `Canvas/EscSettingsRoot/SettingPanel/Line` | Sliced | 4 |
| `Canvas/resourcesUI/Line` | Sliced | 3 |
| `Canvas/SettlementHUD/MainPanel/Player_Select/Line` | Sliced | 4 |
| `Canvas/SettlementHUD/MainPanel/PreViewImage/Line` | Sliced | 4 |
| `Canvas/SettlementHUD/Repair_HUD/Bg/Line` | Sliced | 4 |
| `Canvas/SettlementHUD/Repair_HUD/Cost/LeftPanel/Line (1)` | Sliced | 4 |
| `Canvas/SettlementHUD/Repair_HUD/line/line (1)` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/LeftPanel/Cost/LeftPanel/Line (1)` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/LeftPanel/Line (1)` | Sliced | 3 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Line` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Mucin_Panel/SharedScrollView` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Share_Panel/SharedScrollView` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/Panel/Sniper_Panel /SharedScrollView` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/TraitEntryTemplate/InactiveRoot` | Sliced | 4 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/TraitEntryTemplate/SelectedRoot` | Sliced | 4 |

### `Assets/Dark UI/Free/B.png`

| Hierarchy path | Image type | PPU multiplier |
|---|---|---:|
| `Canvas/SettlementHUD/ShipTraitTreePanel/backButton` | Simple | 1 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/MucinButton/Line` | Sliced | 6 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/ShareButton/Line` | Sliced | 6 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/ShotgunButton/Line` | Sliced | 6 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/SniperButton/Line` | Sliced | 6 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/UnButton` | Simple | 1 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/UnlockButton` | Simple | 1 |
| `Canvas/Temp_ClearButton (1)` | Simple | 1 |
| `Canvas/Temp_ClearButton` | Simple | 1 |

### `Assets/Dark UI/Free/Button_Large_A.png`

| Hierarchy path | Image type | PPU multiplier |
|---|---|---:|
| `Canvas/EscSettingsRoot/SettingPanel/CloseButton` | Sliced | 1 |

### `Assets/Dark UI/Free/Button_Small_A.png`

| Hierarchy path | Image type | PPU multiplier |
|---|---|---:|
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayConfirmation/ConfirmationPanel/KeepDisplayButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayConfirmation/ConfirmationPanel/RevertDisplayButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/ApplyDisplayButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FrameLimitDropdown` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FullscreenDropdown` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/ResolutionDropdown` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTabButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/GameplayTab/ReturnToMainMenuButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/GameplayTabButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/Cancel/MenuBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/DashBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/DialogueAdvanceBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/DismantleBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/FireBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/InstantRadarScanBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/InteractBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/InventoryBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/MapBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/MoveDownBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/MoveLeftBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/MoveRightBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/MoveUpBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/RadarToggleBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/ReinforcementBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/ResetBindingsButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/RouteAddBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/RouteClearBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTab/RouteRemoveBindingButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/KeyboardTabButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/OptionsBackButton` | Sliced | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/SoundTabButton` | Sliced | 8 |
| `Canvas/resourcesUI/ResourceStrip/CoreResource` | Sliced | 16 |
| `Canvas/resourcesUI/ResourceStrip/ScrapResource` | Sliced | 16 |
| `Canvas/resourcesUI/ResourceStrip/StabilizedAlloyResource` | Sliced | 16 |
| `Canvas/SettlementHUD/AddButton` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchiveNavigationButton` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/Back` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_access` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_analysis1` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_analysis2` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_ancient` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_ending` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_opening` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_radar` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_ready` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_relay` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_rescue` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_restored` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_settlement` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_supply` | Sliced | 8 |
| `Canvas/SettlementHUD/DialogueArchivePanel/RecordList/Content/Record_takeover` | Sliced | 8 |
| `Canvas/SettlementHUD/HangarNavigationButton` | Sliced | 8 |
| `Canvas/SettlementHUD/LaunchButton` | Sliced | 8 |
| `Canvas/SettlementHUD/MainPanel/Player_Select/ship_choiceButton` | Sliced | 8 |
| `Canvas/SettlementHUD/OptionButton` | Sliced | 8 |
| `Canvas/SettlementHUD/Repair_HUD/BackButton` | Sliced | 8 |
| `Canvas/SettlementHUD/Repair_HUD/FacilityManagementButton` | Sliced | 8 |
| `Canvas/SettlementHUD/Repair_HUD/RouteCoreDeckButton` | Sliced | 8 |
| `Canvas/SettlementHUD/Repair_HUD/UpgradeButton` | Sliced | 8 |
| `Canvas/SettlementHUD/SectorTechnologyNavigationButton` | Sliced | 8 |
| `Canvas/SettlementHUD/SectorTechnologyPanel/TechnologyCatalog/TechnologyCardTemplate` | Sliced | 8 |
| `Canvas/SettlementHUD/SectorTechnologyPanel/UpgradeButton` | Sliced | 8 |
| `Canvas/SettlementHUD/SettlementButton` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/mg_dash_missile_salvo` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/mg_guidance_control` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/mg_midrange_pressure` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/mg_salvage_sweep` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/mg_stable_feed` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/mg_sustained_harvest_fire` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/mg_terminal_guidance` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sg_breaching_drive` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sg_choke_barrel` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sg_close_harvest_burst` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sg_close_quarters_overpressure` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sg_extra_pellet` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sg_taunt_resonator` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_accelerator_coil` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_active_cooler` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_bulkhead_cargo_frame` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_cargo_bay` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_collection_route` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_combat_gyro` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_cutting_ammo` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_dash_capacitor` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_engine_tuning` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_lightweight_cargo` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_longshot_stabilizer` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_periodic_reflector` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_radar_amplifier` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_range_focusing` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_rapid_feed` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_reinforced_plating` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_repair_foam` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_return_container` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_return_protocol` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_salvage_magnet` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_salvage_protocol` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_scrap_sorter` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_standard_upgrade` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_survival_protocol` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_targeting_bus` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/shared_vector_thruster` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sn_charge_accelerator` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sn_dash_echo_shot` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sn_focus_lens` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sn_high_output_core` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sn_piercing_amplifier` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sn_semi_auto_laser` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Catalog/Content/sn_stealth_scan` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/ClearSlot` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Inspection/Activation` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/MucinButton` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/ResearchSpecial` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/ShareButton` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/ShotgunButton` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot0` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot1` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot10` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot11` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot2` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot3` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot4` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot5` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot6` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot7` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot8` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/Slot9` | Sliced | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/EquipmentDevelopment/SniperButton` | Sliced | 8 |

### `Assets/Dark UI/Free/CIRCLE4PXSMA.png`

| Hierarchy path | Image type | PPU multiplier |
|---|---|---:|
| `Canvas/SettlementHUD/ShipTraitTreePanel/TraitEntryTemplate/LockRoot` | Simple | 1 |

### `Assets/Dark UI/Free/Divider.png`

| Hierarchy path | Image type | PPU multiplier |
|---|---|---:|
| `Canvas/SettlementHUD/Repair_HUD/Bg/Divider (1)` | Simple | 8 |
| `Canvas/SettlementHUD/Repair_HUD/Bg/Divider` | Simple | 8 |
| `Canvas/SettlementHUD/ShipTraitTreePanel/LeftPanel/Divider` | Simple | 8 |

### `Assets/Dark UI/Free/Down Arrow.png`

| Hierarchy path | Image type | PPU multiplier |
|---|---|---:|
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FrameLimitDropdown/Arrow` | Simple | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/FullscreenDropdown/Arrow` | Simple | 8 |
| `Canvas/EscSettingsRoot/SettlementSharedOptionsModal/OptionsPanel/DisplayTab/ResolutionDropdown/Arrow` | Simple | 8 |

### `Assets/Dark UI/Free/Left Arrow.png`

| Hierarchy path | Image type | PPU multiplier |
|---|---|---:|
| `Canvas/SettlementHUD/MainPanel/PreViewImage/Arrow_Left` | Simple | 8 |
| `Canvas/SettlementHUD/Repair_HUD/line/PreViewImage/Arrow_Left (1)` | Simple | 8 |

### `Assets/Dark UI/Free/Right Arrow.png`

| Hierarchy path | Image type | PPU multiplier |
|---|---|---:|
| `Canvas/SettlementHUD/MainPanel/PreViewImage/Arrow_Right` | Simple | 8 |
| `Canvas/SettlementHUD/Repair_HUD/line/PreViewImage/Arrow_Right (1)` | Simple | 8 |

## Authoring and preservation

`DarkUISettlementAuthoring.Run` configures source importers, edits only existing saved Images/Buttons and the documented text padding, validates required roles, and records exact usage. It refuses unsaved scenes and Play/Prefab Mode. Missing references fail with a hierarchy path; there is no runtime lookup, fallback skin, hierarchy builder or manager.

`StructuralFrameAuthoring.AuthorSettlement` reapplies the equipment role styling after its existing layout refresh. EquipmentDevelopmentInstaller already calls this method. EquipmentEconomyAuthoring only touches its existing cost rows and does not overwrite these backgrounds. No retired installer menu was restored. The new regression tests check repeat authoring, component IDs, owner records and persistent callbacks.

The current-tree preservation audit retains all **5,000 existing scene object/component IDs**, all owner records, callbacks and navigation. All 65 source PNGs remain byte-identical. Route Core world area, SettlementDefense encounter, enemy/enemy-base/boss assets, gameplay/save/progression, manufacturing/fitting, Structural Frame balance, cargo rules and the existing inventory working-tree edits are unchanged.

## Validation and remaining limits

Unity 6000.0.69f1 executed successfully; licensing did not block this pass. Runtime and Editor/test assemblies compiled. Localization validation passed for the existing 245-record catalog; no localization source/catalog change or import was needed.

- New DarkUI tests: **70/70 passed**.
- Final focused Settlement selection, as executed in the full suite: **308/311 passed**. The earlier separate focused invocation passed 306/309 before the two marker tests were added.
- Full EditMode: **839/842 passed**. The only failures are `SettlementAdditionalTraitsUIAuthoringTests.AnalyzedOldSavesDeriveUnlocksWithoutGrantReceiptOrCurrencyMutation(3/5/7)`. These are the same existing sectorTechnologyLevels save-normalization failures independently reproduced before this pass; no new failures were introduced.
- Serialized audit: all 5,000 scene IDs preserved, no hierarchy/component additions or removals, owner records/callbacks/navigation unchanged, all 65 image GUIDs and source bytes preserved. The only runtime source change is the Recovery requirement marker string; its condition evaluation, colors and authority are unchanged.
- Task-scoped whitespace checks are clean. A whole-tree scene diff reports two trailing-whitespace lines on serialized component 970400009 that were already present in the starting local tree; they were preserved.
- **28 actual 480x270 Play Mode captures**, 14 each in Korean and English: Hangar, navigation focus, equipment, locked equipment, manufacturing, Structural Frame detail, Special detail, Ship Reinforcement, Recovery, Route Core, archive, Settings gameplay, Settings sound and Settings display. The probe uses the saved scene UI and disposable progression data without a SaveManager or world startup. All final sampled TMP overflow and missing-glyph checks are clear.
- Actual EventSystem pointer click and keyboard/controller-direction handlers passed; Settings Close restored visible focus in both languages. Existing Settlement navigation/modal tests passed. No physical controller or mouse hardware session was performed.

Rendered review found no new border collapse, decorative-corner stretching or sprite blur. The reinforcement baseline correction prevents white cards; selected yellow and blue focus remain visible. The 7.5-point equipment labels retain their font height, with local width compression only when needed. No global font reduction or semantic palette change was made.

Existing visual/content limits remain: Hangar, Settings and several legacy equipment/effect/body strings stay Korean in English mode because those authored/runtime strings do not currently localize. The Hangar cursed-preview sprite is the existing white-square `Assets/Space Kit/Player/Px_Player.png` placeholder, tinted by its existing layers; final ship artwork is outside this pass. The smallest existing sidebar/resource labels are still dense at native resolution. These were not addressed by shrinking text, replacing ship art, or changing the UI structure.

Evidence: `Logs/DarkUISettlement/FullEditMode.xml`, `Focused.xml`, `AuthoringFinal.log`, `RenderFinal.log`, `Rendered/audit.txt`, the 28 PNGs in `Rendered/`, `production-usage.tsv`, `preservation.txt`, and `scoped-diff-check.txt`. Logs are ignored local artifacts. `contact-sheet.png` records the complete source inspection.

## Changed files

- `Assets/01_Scenes/Settlement.unity`: existing UI Images, Button tints, 12 equipment label padding/width settings, the Recovery result text rectangle and archive title bounds (clear of the resource strip). No gameplay owner binding changed.
- `Assets/Dark UI/Free/*.png.meta`: all 65 importers listed above; original PNGs and vendor code unchanged.
- `Assets/02_Scripts/UI/Editor/DarkUISettlementAuthoring.cs` and `.meta`: explicit saved authoring, import configuration, validation and usage export.
- `Assets/02_Scripts/UI/Editor/DarkUISettlementRenderProbe.cs` and `.meta`: opt-in disposable Play Mode capture and focus/glyph checks.
- `Assets/02_Scripts/UI/Editor/Tests/DarkUISettlementTests.cs` and `.meta`: 70 focused tests.
- `Assets/02_Scripts/UI/Editor/StructuralFrameAuthoring.cs`: preserve the equipment skin when existing authoring runs again.
- `Assets/02_Scripts/Settlement/SettlementController.cs`: ASCII Recovery status marker only.
- This audit, `SYSTEM_DESIGN.md`, `DESIGN_CHANGELOG.md`, and `IMPLEMENTATION_STATUS.md`.

No production prefab, Route Core world-area asset, enemy, enemy base, boss, save DTO or gameplay balance asset was changed. No commit/push.
