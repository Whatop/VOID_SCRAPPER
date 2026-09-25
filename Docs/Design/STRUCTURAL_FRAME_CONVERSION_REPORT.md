# Structural Frame Equipment Conversion — implementation report

Date: 2026-09-25. Worked from the current local tree. No commit or push. The pre-existing deletion of `VOID_SCRAPPER.slnx` is preserved.

Implementation and saved-asset authoring are finished. Unity-dependent validation is blocked by licensing initialization; no rendered Play Mode success is claimed.

## Selected-ship branches and Shared Row 2

Equipment Development now exposes Shared plus the current Hangar-selected ship: Sweeper, Breacher or Lancer. It uses PermanentProgress's existing selected ship/WeaponTree and the existing change event. Hidden authored tabs/data remain; an invalid current tab falls back to Shared. Inspecting equipment never changes the actual ship. Cross-ship fitting preferences remain stored and effective deployment remains compatibility-filtered.

| Shared Row-2 position | Equipment ID | Recipe | Maximum level |
|---|---|---|---:|
| Lightweight Frame | `shared_lightweight_frame` | 20 Scrap | 1 |
| Standard Frame | `shared_standard_frame` | 18 Scrap | 1 |
| Heavy Frame | `shared_heavy_frame` | 18 Scrap + 1 Core | 1 |

All three unlock together after first completed component analysis. Normal save-first manufacturing pays exactly once; fitting is free and unrestricted. None, any single, any pair and all three are supported. Lv1 deployment reaches MAX immediately; existing MaxLevel eligibility excludes them from ordinary upgrade offers at deployment. No reward/shop/tuning caller was changed. The shared RunRuntimeTraitStore.CanUpgrade check retains the snapshot-owned structural Lv1 floor after runtime removal, preventing wasted re-upgrade offers while the structural profile is still active.

The replaced `shared_salvage_protocol`, `shared_reinforced_plating`, and `shared_repair_foam` remain legacy-owner equipment. Only their normal-roster flags changed. Existing owners retain fitting and effects; fresh saves cannot research/manufacture them. There is no refund, deletion or automatic substitution.

## Exact fusion profiles

One row applies in full. Individual frame LevelEffects are empty, preventing additional single-module stacking. Values remain provisional.

| Fitted frames | Move speed | Dash cooldown | Dash distance | Max HP | Cargo | Damage | Harvest yield |
|---|---:|---:|---:|---:|---:|---:|---:|
| None | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| Lightweight | +12% | -15% | +0.60 | -3 | -20 | 0 | 0 |
| Standard | 0 | 0 | 0 | 0 | +10 | +5% | +5% |
| Heavy | -10% | +15% | 0 | +6 | +30 | 0 | 0 |
| Lightweight + Standard | +8% | -10% | 0 | -2 | -5 | +3% | +3% |
| Lightweight + Heavy | +4% | 0 | +0.30 | +2 | +10 | 0 | 0 |
| Standard + Heavy | -6% | +8% | 0 | +4 | +25 | +3% | +3% |
| Lightweight + Standard + Heavy (Integrated) | +3% | 0 | +0.15 | +3 | +15 | +3% | +3% |

The integrated frame is broader than each specialist but below Lightweight mobility, Standard damage/yield and Heavy HP/cargo. No ordinary equipment-count scaling remains.

## Runtime and reconstruction authority

RunContext snapshots the structural bit set from its immutable effective fitting at fresh-run creation. It never resolves from runtime levels, live fitting or total item count. Dismantling and portal transitions cannot change the snapshot.

PlayerRuntimeStatApplier is the existing effect owner: base ship -> exact structural profile -> existing technology/story modifiers -> stat commit. ExpeditionBootstrap retains Reinforcement/region/runtime Trait reconstruction and restores carried depleted vitals last. HP, cargo and dash distance use the existing rebuild path; movement/cooldown use source-owned external multipliers; damage and yield use their existing player modifier components. No ship base assets, new manager, Update loop, extra armor or Reinforcement charges were introduced.

Run-end cleanup and next-run Lv1 initialization remain with the existing player/run lifecycle. Ordinary in-run dismantling/drop behavior is preserved; the immutable structural profile survives those level changes.

## Retired selector and presentation

The separate OperatingFrameType/profile and PermanentProgress preference/setter are removed. The Settlement selector and its 33 serialized object/component IDs are removed, with no hidden live duplicate controls. The existing TAB inspection was converted to structural profiles; its saved bindings are preserved in the shared inventory prefab and Expedition's unpacked copy. Former serialized names are retained only as compatibility attributes.

Normal frame cards show a structural Shared tag, Max Lv.1, single effects, fusion explanation, current profile and projected combination after fitting. Hover/focus remains blue, selection yellow, and fitting has its own indicator. The read-only build header shows the resolved profile and opens its actual modifier details. F10 reports manufactured/fitted structural modules and the effective combination; there is no obsolete selection setter.

## Save migration and economy

Save version 8 consumes the version-7 numeric field: Standard 0, Lightweight 1, Heavy 2. A valid recorded selection converts to exactly one corresponding manufactured + fitted module only when Sector Stabilizer is owned and DeepZone1 analysis unlock is complete. No currency is charged. Before Row 2, absent pre-frame fields and invalid values grant nothing.

The migration-only integer tombstone resets to -1. Explicit DTO initialization plus JsonUtility overwrite protects absent-field defaults. Version 8 cannot regrant or refit the old default Standard across reloads. Existing currency, campaign progress, roster manufacturing/fitting, grandfathered research and starter receipts are retained. Earlier equipment migrations remain intact.

The replacement row still totals 56 Scrap / 1 Core. Other 45 final board assets/recipes are unchanged. Starter six still cost 74 Scrap, with an exact-once 86 Scrap / 0 Core allowance and no structural modules included.

## Files, scenes and assets

- Runtime/data: StructuralFrameProfile (renamed from OperatingFrameType), RunContext, PlayerRuntimeStatApplier, RunRuntimeTraitStore, PermanentProgress and its equipment partial, SaveData, SaveManager.
- UI: ShipTraitTreePanel equipment partial; removed its old OperatingFrames partial; StructuralFrameText; PlayerBuildStatusPanelUI and its converted inspection partial; F10 campaign reporting.
- Authoring/diagnostics: StructuralFrameAuthoring (converted from OperatingFrameAuthoring), EquipmentDevelopmentInstaller, EquipmentFinalRosterAuthoring, EquipmentEconomyDiagnostics.Model.
- Assets: three new TraitDefinition assets 67–69 plus metas; three legacy roster flags; TraitCatalog_Main; localization CSV/catalog. Existing GUIDs retained.
- Saved UI: Settlement.unity, Expedition.unity, PF_ExpeditionMapInventoryMenu.prefab. Tutorial inherits the shared prefab.
- Tests: 39 focused structural cases replace the old selector cases; existing catalog/economy/equipment tests updated for the new contract. Standalone production-data checks are in `Tools/Validation/StructuralFrameDataChecks.cs`.
- Documentation: EQUIPMENT_FINAL_ROSTER, EQUIPMENT_DEVELOPMENT_MAPPING, EQUIPMENT_ECONOMY, SYSTEM_DESIGN, DESIGN_CHANGELOG, IMPLEMENTATION_STATUS and this report.

## Executed validation

- Runtime and Editor/test static compilation against the installed Unity 6000.0.69f1 references: **0 errors**, with the existing 52 runtime / 4 Editor warnings.
- Standalone execution of the production resolver, SaveData migration and real enum definitions: **349 assertions passed**. Covers all eight exact profiles, duplicate/unknown IDs, 0/6/12/18/24/38 ordinary-module independence, migration version/selection/research boundaries, idempotence, no refitting, currency and campaign preservation. This does not substitute for Unity JSON or lifecycle execution.
- Saved-asset/preservation audit: **418 checks passed**. Confirms 69 unique catalog definitions/GUIDs, 48 unique positions (12/12/12/12), all three Row-2/Max1/empty-effect recipes, all other 45 board definitions unchanged, three legacy assets changed only in roster membership, four story/boss assets unchanged, retained authored tab/inventory references and no new dangling local serialized references.
- Localization: 234 source/catalog records, 29 structural text keys with Korean/English text and matching named placeholders; generated ordering and hash updated. Hash algorithm cross-checked against the previous generated catalog. Full Unity importer validation is pending.
- Scoped Git whitespace checks completed. Existing solution deletion preserved; no commit or push.

## Unity and actual rendered Play Mode checks

Unity was closed at task start, so there was no unsaved Editor scene/prefab work to overwrite. The validation process launched for this pass reported `Connection to channel LicenseClient-whdgk refused` and stalled at `Licensing is not yet initialized`, before project compilation or the authoring method. The task-owned stalled process was stopped. Saved asset updates were applied from the inspected disk state and audited structurally.

**Unity EditMode execution: not completed. Actual rendered 480×270 Play Mode checks: not performed. No screenshots or successful fresh-run/portal playthroughs are claimed.** Earlier document counts/screenshots are historical and were not reused as evidence for this pass.

Pending licensed-Editor checks:
1. Run the full EditMode suite, including SettlementAdditionalTraitsUIAuthoringTests.
2. At 480×270 switch Sweeper/Breacher/Lancer and inspect Shared frames and hidden-branch fallback.
3. Manufacture/fit Standard, launch, and verify its exact live effects and MAX exclusion from offers.
4. Launch Lightweight + Standard and all-three combinations; inspect fusion values and scroll readability.
5. Portal with depleted HP/Armor/charges and cargo; verify no stacking/refill/cargo loss.
6. Return, switch combination and relaunch; verify the new profile.
7. Verify a legacy Salvage Protocol owner and a fresh save, plus an actual version-7 JSON reload/migration.

## Remaining balance concerns and preserved scope

Natural movement/dash feel, Lightweight's reduced reserve, Heavy plus overload penalties, all-three versus specialist value, legacy stat stacking and the revised early-game absence of a free Standard frame need playtesting. Old count-based economy tables are marked historical, not validation of the new profiles. Dedicated structural artwork was not added; existing icons are reused.

Existing story/boss Traits, reward callers, other 45 board items, starter economy, campaign rules and authored ship bases remain unchanged. **The post-ending Curse system was not implemented.**
