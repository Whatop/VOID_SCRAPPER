# Story Recovery and Research Special Equipment — Completion Report

Date: 2026-09-25. Work used the current local working tree, including the earlier structural-frame and inventory passes. No commit or push.

## 1. Outcome

Implemented three research-derived Special equipment blueprints and replaced the always-visible inventory recovery strip with an optional read-only Recovery log. Equipment and Cargo each use a 418 × 196 body, increased from 418 × 164.

Code and saved content are authored. Static and standalone validation passed. Unity licensing blocked generated localization import, EditMode execution and rendered Play Mode checks; these remain outstanding.

## 2. Implemented design decisions

Sector Stabilizer, Matter Compressor and Phase Navigation Lens remain campaign progression data. Original parts have no equipment definitions or field-drop actions. Boss acquisition, world recovery, analysis, route authorization and assembly authority are unchanged.

The resulting modules use existing Special rarity and Shared compatibility. They are passive Trait equipment in normal Equipment storage/detail. Active Reinforcement retains its separate slot. All three modules may be fitted together without an extra cap.

| Blueprint ID | English name | Required analysis | Accent | Scrap / Core |
|---|---|---|---|---:|
| special_sector_stabilization | Stabilized Return Module | Sector Stabilizer | Orange | 24 / 1 |
| special_matter_compression | Matter Compression Module | Matter Compressor | Green | 32 / 2 |
| special_phase_navigation | Phase Navigation Module | Phase Navigation Lens | Blue | 40 / 3 |

All have MaxLevel 3. These increments apply at each level through existing effects:

- Stabilized Return: Max HP +1, healing efficiency +5%, Emergency Return capacity ratio +5 percentage points.
- Matter Compression: cargo +6, harvest yield +3%, pickup range +0.20.
- Phase Navigation: radar radius +1, range +5%, spread reduction +3%.

Existing multiplicative percentage semantics remain authoritative. Effects use the common stat palette; orange/green/blue identify rarity accents. Existing Reinforced Plating, Cargo Bay and Radar Amplifier icons are reused. No new manager, combat framework, active slot or currency.

A compact Special research button under Shared switches the existing card/inspection area to the three blueprints. Shared plus selected-ship filtering remains. The 48-position board, seventeen legacy Shared modules and starter allowance are unchanged. Catalog total: 72 definitions, including 68 ordinary-flow equipment, three unchanged hidden boss protocols and the existing story Curse definition.

## 3. Files changed

Relative to this pass's current-tree starting snapshot:

- Equipment authority: `TraitDefinition.cs`, `PermanentProgress.Equipment.cs`, `RunRewardOption.cs`.
- Settlement UI/authoring: `ShipTraitTreePanel.Equipment.cs`, `EquipmentDevelopmentInstaller.cs`, `EquipmentFinalRosterAuthoring.cs`, `StructuralFrameAuthoring.cs`.
- Inventory: `PlayerBuildStatusPanelUI.cs`, `PlayerBuildStatusPanelUI.InventoryTabs.cs`, new `PlayerBuildStatusPanelUI.StoryProgress.cs` and metadata.
- Saved UI: `Assets/01_Scenes/Settlement.unity`, `Assets/01_Scenes/Expedition.unity`, `Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab`. Tutorial retains its prefab reference.
- Content: new `Common/70_special_sector_stabilization.asset`, `71_special_matter_compression.asset`, `72_special_phase_navigation.asset` and metadata; `TraitCatalog_Main.asset`; localization source `Localization.csv`.
- Tests: new `SettlementAdditionalTraitsUIAuthoringTests.ResearchSpecial.cs`; updated `SettlementAdditionalTraitsUIAuthoringTests.Catalog.cs`, `InventoryPresentationTests.cs`, `ExpeditionUIAuthoringTests.cs`, `QAStabilizationPass1Tests.cs`.
- Validation: `Tools/Validation/ResearchSpecialAssetChecks.ps1`, `ResearchSpecialContentChecks.cs`, `ResearchSpecialContentChecks.ps1`.
- Documentation: `SYSTEM_DESIGN.md`, `DESIGN_CHANGELOG.md`, `IMPLEMENTATION_STATUS.md`, `EQUIPMENT_FINAL_ROSTER.md`, `EQUIPMENT_DEVELOPMENT_MAPPING.md`, `EQUIPMENT_ECONOMY.md`, this report.

Starting snapshot and evidence: `Logs/ResearchSpecialBaseline` and `Logs/ResearchSpecialValidation`. Earlier working-tree changes and the pre-existing solution-file deletion were retained.

## 4. Data / migration behavior

No save fields or version bump; version remains 8.

Each definition's `requiredStoryPartAnalysis` references the existing campaign enum. `PermanentProgress.HasCompletedStoryPartAnalysis` derives eligibility from actual part ownership and completed-analysis authority. The first two analyses use authorized depth; final analysis uses its existing completion flag. Existing load normalization already restores that flag for older assembled/activated saves.

Previously completed analyses expose blueprints on load. Unlock queries do not grant equipment, fit it, charge currency or create migration receipts. Repeated loading cannot duplicate a grant because no automatic grant occurs. Acquisition without analysis stays locked.

Manufacturing/fitting use existing atomic saved transactions and ID lists. Fresh runs snapshot fitting and deploy Lv1. Upgrades and portal reconstruction remain in RunRuntimeTraitStore, RunTraitEffectApplier and existing player/bootstrap owners. No parallel runtime authority was added.

## 5. UI behavior before versus after

| Before | After |
|---|---|
| Recovery strip permanently above tabs | Compact Recovery log button opens original read-only slots |
| Both tab bodies 418 × 164 | Both bodies 418 × 196, with larger storage/detail and cargo rows |
| Story items consume the top strip | Equipment/Cargo occupy the body; progression is inspected on demand |
| No research-derived blueprint cards | Shared Special subsection uses normal inspection/manufacturing/fitting |
| Status shows acquisition | Read-only inspection distinguishes acquired, unacquired and analyzed |

The recovery overlay blocks equipment/cargo actions, contains keyboard navigation on its close button, clears held jettison progress and restores tab focus. It closes with the inventory and on tab switches. Acquisition never opens it automatically. Original slot IDs/campaign references remain intact.

Cargo types, jettison quantities, auto-pickup, repickup rules, equipment removal, Map lifecycle, pause/cursor ownership and Active Reinforcement charges remain with their existing authorities.

## 6. Validation performed

Passed:

- Runtime and Editor/test compilation using Unity 6000.0.69f1 Roslyn/project references: **zero errors**, 52 runtime and 4 Editor warnings, matching earlier counts.
- **8,671 serialized asset assertions**: unique file IDs, bindings/references, recovery hierarchy, tab dimensions, Tutorial linkage, recipes/levels/gates, 48-board preservation and unchanged pre-existing assets/runtime/save files.
- **58 production content/localization assertions** using actual TraitDefinition and localization parser/validator/lookup with thin engine adapters. All **245 CSV records** validated. New names/descriptions resolved in Korean and English against an in-memory catalog; the generated asset was not written.
- **767 production presentation assertions** covering all 49 Trait and 25 Reinforcement effects and structural-profile presentation.
- Authored and compiled **25 new NUnit cases** covering analysis-only unlocks, manufacturing/fitting/reload, old saves, no grants, all-ship compatibility, Lv1 deployment, upgrades, immutable fitting, depleted-vitals portal reconstruction, next-run reset, Settlement discovery and overlay safety. Earlier catalog/hierarchy assertions were updated.

Not executed: Unity EditMode cases, actual rendered 480 × 270 pre/post-unlock/manufacture/inventory/deployment/upgrade checks, Korean/English rendered bounds, screenshots, generated localization import.

Batch import could not connect to `LicenseClient-whdgk`; failed reconnect logs also report a missing headless entitlement. Only the task-owned Unity process was stopped. Evidence: `Logs/ResearchSpecialValidation/unity-import.log`. Serialized geometry is not rendered validation.

## 7. Remaining risks / manual checks

1. In a licensed Editor, run **VOID SCRAPPER > Localization > Import Catalog**. Generated localization stayed unchanged from this task's starting tree. New module names/descriptions fall back to authored Korean until import; Korean/English source keys are present and validated.
2. Run the focused SettlementAdditionalTraitsUIAuthoringTests, InventoryPresentationTests, ExpeditionUIAuthoringTests and StoryRecovery QA tests.
3. At actual 480 × 270, verify locked/analyzed cards, exact-once purchase, fitting, all three runtime entries at Lv1, upgrades, depleted-vitals portal handoff, next-run reset and the optional Recovery log in Tutorial/Expedition. Check English card wrapping and capture real screenshots.
4. Natural playtesting should assess new effects and optional purchase pacing. Dedicated icons remain an art follow-up.

Existing equipment effects/recipes, structural-frame balance, cargo rules, enemy bases, bosses and Active Reinforcement gameplay were not changed. Post-ending Curse was not implemented. No commit or push.
