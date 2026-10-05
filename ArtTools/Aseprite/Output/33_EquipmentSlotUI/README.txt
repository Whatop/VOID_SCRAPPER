VOID SCRAPPER - EQUIPMENT FRAME / SLOT UI PACK

UI_EquipmentSlot_64.aseprite contains four STATIC 64x64 states:
  Idle     - the approved neutral frame, preserved pixel-for-pixel
  Selected - four cyan/white focus brackets, inward pointers and a focused top edge
  Equipped - persistent top check tab and a short muted-green active line
  Locked   - darker desaturated rails with a clear top padlock tab

The four single-frame tags are Idle, Selected, Equipped and Locked. They are state
choices, not a four-frame animation. Each state has its own transparent PNG.
UI_EquipmentSlot_64.png is the corresponding 256x64 horizontal state sheet.
UI_EquipmentSlot_64.json includes frame rectangles, tags and assembly/import notes.

EDITABLE LAYERS
  Shared Frame Rails
  Shared Corner Hardware
  State Overlay
  Persistent Marker

The common frame was extracted from a verified copy of the approved 64px source.
Only the new pack is edited. Existing equipment frames, icons, modals and other
approved assets remain unchanged.

ICON WINDOW
All four states leave x12..51, y12..51 completely transparent: a 40x40 window.
Recommended item placement: a centered 32x32 icon at x16, y16, leaving 4px padding.
No icon, text, panel fill, rarity effect or soft glow is baked into any slot export.
The badge stays above the icon window, so lock/check identification never covers
the item. Locked dims only the frame; item tint remains under the UI caller's control.

EXPORT OPTIONS
Use one complete state PNG, or assemble separate parts:
  UI_EquipmentSlot_64_Base.png - common approved Idle frame
  UI_EquipmentSlot_64_SelectedOverlay.png
  UI_EquipmentSlot_64_EquippedOverlay.png
  UI_EquipmentSlot_64_LockedOverlay.png
  UI_EquipmentSlot_64_EquippedMarker.png
  UI_EquipmentSlot_64_LockedMarker.png

All overlays are 64x64 and use the same pivot as Base. Markers are cropped to
16x12; place their top-left at x24, y0 relative to the 64px frame. Base + the chosen
state overlay + its marker reassembles exactly to the corresponding full state.
Idle needs only Base; Selected needs no persistent marker.

Selection and equipment ownership can coexist. To show an equipped item focused
by the cursor, draw Base, SelectedOverlay, EquippedOverlay, then EquippedMarker.
The persistent check stays visible and the central icon window stays clear.
Do not stack a complete state PNG underneath its duplicate component exports.

IMPORT
Use Point filtering, no mipmaps, lossless/uncompressed pixels, alpha transparency
and a centered pivot. Display at 64x64 at the UI's native scale with integer-aligned
positions. The shared rails use the existing 2px construction; tiny status glyphs
use crisp 1px steps. All alpha is binary. No gradients or anti-aliasing are used.
Unity owns state selection, actual item icons, input, tint and any UI animation.

REVIEW FILES
EquipmentSlot_Comparison.png compares all four native frames with the SAME example
icon, followed by an exact 2x enlargement for marker and corner inspection.
EquipmentSlot_480x270.png is a native-size mockup with example equipment placement.
Approved 64px icon references are sampled at 32px using nearest-neighbor for these
previews only. No reference source is modified and no icon is baked into frame art.
The previews are Aseprite art compositions, not Unity Play Mode captures.

VALIDATION
16,384 source/export pixel comparisons passed for the four states, plus individual
PNG/sheet agreement, frame tags/layers, palettes, binary alpha, clear icon windows,
unchanged icon pixels, and exact assembly from the exported components.
Selected adds 138 visible focus pixels and has 1.63x Idle's total edge luminance;
Locked has 0.70x Idle's mean frame luminance. These checks supplement native review.
Each state uses 3-6 colors. The approved Idle frame is retained exactly.
All 1,076 protected input, output, script and reference files remained unchanged.
See validation.json and protected_hashes.json. No Unity assets or code were changed.

REBUILD
From the project's ArtTools/Aseprite directory:
  & '.\Scripts\Generate-EquipmentSlot.ps1' -ReplaceGenerated
The runner verifies exact reference paths/hashes, makes fresh Temp copies, checks
UTF-8 without BOM, runs Aseprite CLI + Lua, then validates outputs and preservation.
It targets only this generator's Output/33_EquipmentSlotUI folder. The unchanged
shared pixel helper is included in the archive; logs and copied references remain
under Temp/equipment_slot_*. The installed PixelLab extension's existing
handle-pose.lua startup warning did not prevent successful source/export validation.
