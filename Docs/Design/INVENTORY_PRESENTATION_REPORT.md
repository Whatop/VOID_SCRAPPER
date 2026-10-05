# Inventory presentation - final visual polish

Completed: 2026-09-28 KST; started 2026-09-27. Work began from the current local working tree. The pre-existing untracked `VOID_SCRAPPER.slnx` is preserved. No commit or push.

## Cargo default and filter

Every new PlayerBuildStatusPanelUI starts with Show All enabled. Scrap, Core and Stabilized Alloy remain visible at zero, with Scrap selected first. The established selection repair preserves a visible selection and chooses the first visible owned resource when filtering hides it. The three-row serialized order remains Scrap / Core / Stabilized Alloy.

The button describes the action: `[보유만 보기]` / `[Owned Only]` in Show All, and `[전체 보기]` / `[Show All]` in Owned Only. Show All has no redundant empty message; Owned Only with no resources has one empty message and no rows. This is per-instance UI state, retained on close/reopen, with no SaveData preference and no cargo grant or pickup change.

## Final hierarchy and equipment

The compact resolved Structural Frame status and Credits/Tuning Chips stay above Equipment, Cargo Resources and Recovery Log. There is no separate Operating Frame selector. Equipment retains Active Reinforcement and four-column Storage on the left and selected detail on the right. Cargo retains its full-body layout. Recovery Log remains an optional read-only overlay.

Tab labels contain no checkmark or other text selection glyph. Existing Button/Image backgrounds, yellow text/outline and blue hover/focus communicate state. Authored Images have a white base so ColorBlock tints are visible.

Active icon, heading, name, effects, charge/recharge state and Drop remain bound. Panel borders, padding, heading baselines and action placement are adjusted. Selected detail prioritizes name, Lv/MAX, rarity, description, colored effects and Drop. Effects increased from 7.5 to 9 authored points, with spacing and a retained ScrollRect. StatPresentation and its semantic palette are unchanged.

Storage remains four columns with consistent 43x34 cells, independent rarity/Frame labels, Lv/MAX labels, and a selected outline. The viewport fits two complete rows and retains scrolling for additional rows. Keyboard selection scrolls its card into view using the existing ScrollRect. The single bottom contextual G hint is neutral/empty without a valid target; drop input and authoritative drop methods are unchanged.

## Cargo and Recovery readability

Manifest rows separate resource name/quantity from unit weight, load contribution and Auto Pickup. Zero quantities dim through opaque readable text and neutral icon modulation; existing colored resource art is retained. Currency identity colors are not mapped to StatPresentation.Cargo. Selection has an authored outline.

The gauge has a visible empty track at zero, with current/max immediately above. Emergency Return shows the existing controller retention limit without repeating current load or calculating a second return result. Selected Cargo separates identity and two-line statistics from its slider, 1/Half/Max, Auto Pickup and Jettison controls. Zero-quantity jettison controls remain disabled.

Recovery Log contains exactly Sector Stabilizer, Matter Compressor and Phase Navigation Lens, analysis count and Close. Orange, green and blue identities use the established generic story-part fallback when dedicated art is missing. States are Not Acquired / Recovered / Analyzed (Korean equivalents); only HasCompletedStoryPartAnalysis permits Analyzed. The modal never writes progression. Its compact panel, padding, dimmer and sole Close focus target are authored.

## Languages, glyphs and input

Korean/English tab, heading, rarity, filter, resource, action and Recovery labels are adjusted. Seven existing Recovery localization records were updated; the source was imported and validated through LocalizationContentImporter. This also installs eleven already-authored Special/Recovery source records that were absent from the starting catalog (245 records after import). Equipment content continues using its existing localization authority. Legacy equipment/Active names, descriptions and some effect lines still fall back to Korean in English; this pass does not invent translations for the full equipment catalog.

Rendered glyph auditing checks active TMP text and font fallback, including Korean, English, numbers, /, +, -, %, Lv., MAX and brackets. Decorative tab glyphs and malformed authored button labels are removed. Navigation links are refreshed at presentation/selection changes, with no new Update loop. Cargo includes the filter, visible rows, enabled slider/presets, Auto Pickup and Jettison; no hidden control is linked. Tabs repair focus and clear carried jettison hold state. Recovery blocks underlying actions and Close restores current visible inventory focus.

## Authored assets and files

- Shared PF_ExpeditionMapInventoryMenu.prefab and Expedition.unity unpacked inventory copy: saved through InventoryPolishAuthoring, preserving existing IDs and references.
- TestSlotButton.prefab: existing storage card, type label and selected outline.
- Tutorial.unity: unchanged; inherited prefab verified.
- Runtime: PlayerBuildStatusPanelUI.cs, InventoryTabs.cs, StoryProgress.cs and BuildStatusSlotButtonUI.cs. StructuralFrames.cs, StatPresentation.cs and ExpeditionMenuController.cs are unchanged.
- Localization.csv and Unity-imported LocalizationCatalog.asset.
- Editor: InventoryPolishAuthoring.cs and InventoryPolishRenderProbe.cs, with generated metas.
- Tests: InventoryPolishTests.cs, ExpeditionUIAuthoringTests.cs and the Recovery assertions in QAStabilizationPass1Tests.cs.
- Validation: Tools/Validation/InventoryAssetChecks.ps1.
- Documentation: this report, SYSTEM_DESIGN.md, DESIGN_CHANGELOG.md and IMPLEMENTATION_STATUS.md.

InventoryPolishAuthoring refuses dirty scenes or active Prefab/Play Mode. No missing panels are generated at runtime. RenderProbe is opt-in Editor validation with transient gameplay owners and no SaveManager; it never saves campaign fixtures.

## Executed validation

| Check | Result |
|---|---|
| Runtime and Editor/test compilation in Unity 6000.0.69f1 | Passed; no compiler errors |
| Focused inventory and authored-UI tests | 58/58 passed, including the final re-imported assets |
| Full EditMode suite | 769/772 passed; three pre-existing failures below |
| Pre-existing failure reproduction | All three fail identically against the starting versions, independently of this polish |
| Authored references, IDs, hierarchy, Tutorial inheritance and unchanged gameplay bodies | 9,495 checks passed |
| Localization import/freshness validation | Passed, 245 records |
| Scoped diff whitespace check | Passed |
| Actual 480x270 Play Mode | 20 captures; no missing glyphs or reported TMP overflow in the sampled surfaces |
| Pointer and directional/submit EventSystem checks | Equipment, Cargo and Recovery passed in both languages |

The remaining failures are `SettlementAdditionalTraitsUIAuthoringTests.AnalyzedOldSavesDeriveUnlocksWithoutGrantReceiptOrCurrencyMutation` at checkpoints 3, 5 and 7. Reload normalization adds existing sector-technology entries, causing the expected serialized-save equality assertion to fail. The same messages and JSON differences occur with the task's nine changed runtime/asset files temporarily replaced by their starting versions, then restored. No save/progression fix was made in this UI pass.

Evidence: `Logs/InventoryPolish/FinalReferences.xml`, `FullEditModeComplete.xml`, `BaselineFailures.xml`, `AuthoringFinal.log`, `RenderedComplete.log` and `Rendered/audit.txt`. Earlier Recovery tests were updated to the intended brighter unacquired state and compact modal's sole Close control; all failures introduced by the presentation change are resolved. The asset audit uses the task-start snapshots in `Logs/InventoryPolish/Baseline`.

Unity saved every authored change. Only serialization ordering/whitespace was normalized afterward to reduce diff noise; all original object IDs remain and the final saved assets passed another 58-case focused run. Unrelated runtime systems, scene objects and the starting untracked solution were preserved.

## Actual rendered 480x270 checks

Twenty production-prefab captures were rendered in Unity Play Mode, at 480x270, and inspected in both languages: ordinary equipment, Special equipment, Structural equipment, zero Cargo Show All, partial Cargo, near-full Cargo, full Cargo, Recovery 0/3, mixed Recovered/Analyzed/Not Acquired, and Owned Only empty. Files and audit are in `Logs/InventoryPolish/Rendered`.

The fixture uses the actual prefab, TMP font, item art, controller readouts and progression query methods with temporary data owners. It is not a whole campaign traversal. Pointer click, directional move and submit handlers are exercised through Unity's EventSystem; physical mouse/gamepad hardware and a live expedition interaction session are not claimed.

## Remaining limits and protected scope

English legacy equipment content retains its existing Korean fallback. More extreme quantities, every long catalog entry, additional accessibility scaling and subjective hardware navigation remain outside the rendered sample matrix. No missing glyphs or TMP overflow were found in that matrix. Dedicated story icons remain optional; the established fallback is displayed.

No gameplay balance, cargo rules, equipment/Trait values, manufacturing, Structural Frame balance, field-drop gameplay, save/progression, enemy-base, boss, combat balance or post-ending Curse changes. The outer Map/Inventory lifecycle remains unchanged. No commit or push.
