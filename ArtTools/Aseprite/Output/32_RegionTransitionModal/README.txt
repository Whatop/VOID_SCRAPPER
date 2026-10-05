VOID SCRAPPER - REGION TRANSITION MODAL UI PACK

THREE STATIC MODAL VARIANTS
  UI_RegionTransition_Base.aseprite / .png
  UI_RegionTransition_SafeReturn.aseprite / .png
  UI_RegionTransition_NextRegion.aseprite / .png

All three use the same 304x168 chassis with a transparent outside margin and an
opaque dark navy panel for readable text. The Base variant has an empty icon well.
Safe Return uses a muted green return/home symbol and calm confirmation accents.
Next Region uses a cyan route/entry symbol and a slightly stronger activation cue.
The frame geometry, body proportions, icon well and text layout are shared.

Each source has one static frame and eight editable layers:
  Panel Fill
  Frame Hardware
  Layout Rails
  Energy Accents
  Icon Well
  State Symbol
  Region Tag
  Continue Button

No title, subtitle, region name, button label, long copy, fade or animation is baked
into the source or exported UI sprites. Unity owns localized text, modal scale-up,
background fade, click behavior and reveal timing. Symbols and energy accents are
small and controlled; no soft glow, anti-aliasing or gradients are used.

TWO EXPORT OPTIONS
1. Use the full variant PNG as one static Image and overlay Unity text.
2. Assemble independently revealable parts:
   UI_RegionTransition_SharedBackdrop.png - shared panel and hardware
   UI_RegionTransition_SharedLayout.png - header/footer fields and empty icon well
   UI_RegionTransition_<variant>_Energy.png - state energy accents
   UI_RegionTransition_<variant>_Symbol.png - 32x32 state symbol (Safe/Next only)
   UI_RegionTransition_<variant>_RegionTag.png - blank 96x22 badge
   UI_RegionTransition_<variant>_Continue.png - blank 112x24 button
Choose one option; do not stack a complete variant underneath its separate parts.
Component exports reassemble exactly to their corresponding full variant PNG.

LAYOUT / LOCALIZATION
layout.json gives top-left pixel coordinates, text-safe rectangles, component
positions and sizes. The icon canvas is 32x32; the region badge accepts runtime
Tutorial / A / B / C / Final labels or another short localized label.
Recommended native placement: panel top-left x88, y51 on a 480x270 UI canvas.
Title area: x80, y28, 200x22. Subtitle: x80, y56, 200x12.
Other text areas cover region label, optional status line, footer and button.
These rectangles are uniform and unobstructed in every exported variant.
The English copy visible in previews is illustrative, not final localization.

IMPORT / SCALE-UP
Use Point filtering, no mipmaps, lossless/uncompressed pixels and alpha transparency.
Set the final modal RectTransform to 304x168 at the project's native UI scale.
Animate the assembled parent in Unity so the frame and child content stay aligned.
Settle at integer screen coordinates for crisp native-size presentation.
If a wider/taller panel is needed, only SharedBackdrop supports nine-slice using
24px borders. Re-anchor the other parts and text; do not slice/stretch a full
composite, icon, badge or button. No Unity import settings were changed here.

REVIEW FILES
RegionTransitionModal_Comparison.png - three raw native-size variants plus examples
with temporary text overlays, explicitly kept separate from deliverable sprites.
RegionTransitionModal_Base_480x270.png
RegionTransitionModal_SafeReturn_480x270.png
RegionTransitionModal_NextRegion_480x270.png
These are native-size art mockups over copied region backgrounds. The uniform
backdrop dimming is for the mockups only; production fade remains a Unity effect.
They are not Unity Play Mode captures or evidence of runtime integration.

VALIDATION / PRESERVATION
153,216 source-to-PNG pixel comparisons passed, together with dimension, palette,
binary-alpha, 2px construction grid, layer/tag, text-window and component assembly
checks. Shared chassis/layout layers match exactly across all three variants.
Base uses 6 colors; each state variant uses 9. Exterior transparency is retained.
All 1,043 protected input, existing output, script and reference files are unchanged.
See validation.json and protected_hashes.json. No approved art or Unity code was
modified, and no runtime animation or gameplay behavior was added.

REBUILD
From the project's ArtTools/Aseprite directory:
  & '.\Scripts\Generate-RegionTransitionModal.ps1' -ReplaceGenerated
The runner checks exact references and hashes, copies them into a fresh Temp run,
checks Lua UTF-8 without BOM, generates with Aseprite CLI and validates the result.
It targets only this generator's Output/32_RegionTransitionModal folder.
The unchanged shared pixel helper is included in the archive. Reference copies
and Aseprite logs are retained under Temp/region_transition_modal_*.
The installed PixelLab extension's existing handle-pose.lua startup warning does
not prevent this build from completing its source/export validation.
