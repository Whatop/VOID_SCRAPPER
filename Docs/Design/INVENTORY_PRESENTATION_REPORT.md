# Inventory UX and Unified Stat Presentation — completion report

Date: 2026-09-25. Work used the current local working tree, including the earlier structural-frame conversion. No commit or push was made.

Implementation and static/standalone validation are complete. Unity test execution and rendered Play Mode validation are blocked by licensing initialization; they are not reported as passed.

## 1. Final hierarchy

```text
InventoryRoot
  Header: resolved-frame inspection + compact Credits / Tuning Chips
  StoryRecoverySection                         (fixed, outside both tabs)
  EquipmentTabButton | CargoTabButton | visible-action hint
  Content
    EquipmentContentRoot                       (initially visible)
      CurrentActivePanel
      EquipmentStoragePanel / existing ScrollRect
      SelectedEquipmentDetailPanel / authored detail ScrollRect
    CargoContentRoot                           (initially hidden)
      Cargo load / gauge / Emergency Return projection
      CargoManifestList
      CargoDetailActions
  StructuralFrameInspection                    (read-only modal)
```

The two content roots reuse the existing column objects and their stable serialized IDs. Both occupy the same 418 × 164 lower body within the 480 × 270 layout target. Story Recovery and outer Map/Inventory ownership remain unchanged.

## 2. Equipment tab

The separate Active Reinforcement area retains its icon, name and recharge display and now binds its existing runtime charge/state/effect data and explicit drop control. Its variable effect text scrolls. Runtime equipment stays in the existing passive storage collection with icon, rarity, selected indicator, current level and an explicit MAX label.

Selected detail includes the existing name, rarity, current/max level, description, current cumulative effects and flavor. Variable detail text scrolls at a larger authored size instead of being compressed into the old shallow strip. Structural modules are ordinary Lv1/MAX cards; selecting one displays the actual immutable fused run profile.

## 3. Cargo tab

Only Scrap, Core and Stabilized Alloy have manifest bindings. Each row displays quantity, unit cargo weight, total contribution and auto-pickup state. Selected-resource controls retain the quantity slider, 1 / Half / Max, jettison and auto-pickup. Empty cargo has a dedicated message and Show All remains available.

Credits and Tuning Chips are displayed in the separate compact header; their old manifest objects remain inactive and retain their serialized IDs. New tab, action and resource labels support Korean and English. Existing equipment names/descriptions and localization authority remain in place.

Emergency Return displays current load and the existing PlayerCargoController.EmergencyReturnCapacityLimit. It does not predict a new distribution of kept resources or duplicate return resolution.

## 4. Input and lifecycle safety

PlayerBuildStatusPanelUI.InventoryTabs owns only presentation state. A new UI instance defaults to Equipment; close/reopen remembers its tab without SaveData. Switching tabs stops storage/detail/Active-effect scroll velocity, closes frame inspection, clears jettison hold progress, selects a valid visible target and repairs focus. A held G must be released before beginning a cargo hold after a switch/open.

Direct Active/passive drop callbacks reject Cargo-tab calls. Cargo selection, quantity, auto-pickup and jettison callbacks reject Equipment-tab calls. Keyboard dispatch routes to the visible tab, so stale selections cannot cross-dispatch. An empty Cargo tab never falls back to a hidden Active target.

The audit compares the actual drop/jettison/auto-pickup method bodies against the starting tree: only visibility guards were added. Pickup spawning, rollback, repickup blocking and Story Recovery operations remain unchanged. ExpeditionMenuController changed only its preferred inventory focus target.

## 5. Shared presentation architecture

StatPresentation is a static utility with semantic categories, colors, rich wrappers and stable summary ordering. It contains no gameplay state or stat authority. TraitEffectTextUtility keeps BuildEffectText and FormatEffect plain; BuildRichEffectText is opt-in. Reinforcement and structural-frame formatters also preserve their plain paths.

The inventory's duplicate Trait formatter now delegates to the shared formatter. This also makes its charge-time display agree with the existing runtime-aware formula used elsewhere. Cumulative equipment effects sort Survival, Combat, Mobility, Salvage, Utility, Special; no empty headings or new global stat collection were added.

Migrated surfaces: Settlement Equipment Development growth rows and controller effect snippets; reward/upgrade choices; selected/current inventory effects; Structural Frame detail/inspection; Active Reinforcement effects and recharge text. All refresh through existing menu/event/selection ownership, with no new Update polling or runtime hierarchy generation.

## 6. Complete palette

| Category | Hex | Category | Hex |
|---|---|---|---|
| Health / Max HP | #FF6B6B | Defense / Shield | #55D6BE |
| Damage | #FF9F43 | Fire Rate | #FFD166 |
| Movement | #9BE564 | Dash | #46E6C8 |
| Cargo capacity | #D7A75E | Harvest / Salvage yield | #6FD08C |
| Projectile Speed | #6CCBFF | Range | #5B8CFF |
| Accuracy / Spread | #7EE7F2 | Homing | #B58CFF |
| Pierce | #D77BFF | Charge | #FF82C8 |
| Radar / Detection / Stealth / Taunt | #44DDE7 | Active / Cooldown | #7AA8FF |
| Healing / Recovery | #70E08F | Special Mechanics | #FFD76A |

Colors communicate category, never benefit/penalty alone. Names, signs, values, units and mechanic descriptions remain explicit. Currency identity colors are separate and unchanged.

## 7. TraitEffectType coverage

All 49 current enum values have explicit mappings. Health, movement, damage, fire rate, projectile speed, range, accuracy, cargo, harvest, recovery and Active effects use their named categories. Dash damage reduction/missile/echo effects remain Dash; charge damage, charge speed/time/size/sight/moving/reserve effects remain Charge. Guidance/distribution use Homing; pierce/falloff use Pierce; scan/taunt/stealth use Radar; reflective shield uses Defense.

Projectile count, suppression, cooling mechanics, twin feed, slug coupling, breach sequence, displacement, semiauto mode and the three story protocols use Special. Mechanic descriptions remain intact. Data-only entries without stat effects use their authored description in inventory; frame modules use the resolved profile. No obtainable effect enum formats as the generic missing-effect message.

## 8. Reinforcement coverage

All 25 effect types map explicitly. Healing and credit-to-heal use Recovery; armor, invulnerability and projectile clearing use Defense. Temporary damage/fire rate/movement/dash/homing/pierce and scrap gain use their corresponding categories. Radar actions use Radar. Knockback, prefab actions, emergency return and marker-return mechanics use Special. Charges, activation, timing and gameplay effects were not changed.

## 9. Structural Frame presentation

None and all seven nonempty exact combinations use the common rich presentation: Lightweight, Standard, Heavy, Lightweight + Standard, Lightweight + Heavy, Standard + Heavy and Integrated. Negative HP/cargo values keep Health/Cargo colors and explicit minus signs. No profile values, fitting rules, immutable snapshot behavior or runtime application paths changed.

## 10. Authored assets

- Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab: tabs, full-body roots, scrolling detail/effects, Active action/status bindings, resource separation, return projection and expanded frame modal.
- Assets/01_Scenes/Expedition.unity: equivalent edits to its unpacked copy, preserving its distinct modal IDs.
- Tutorial.unity is byte-for-byte unchanged from the task baseline and still references the shared prefab.
- No new missing local references or deleted existing object IDs were found. Key tab/hint/projection/body rectangles were checked for overlap; these checks do not prove rendered glyph bounds.

## 11. Code, tests and documentation changed in this pass

Runtime/presentation:
- Assets/02_Scripts/UI/StatPresentation.cs and meta
- Assets/02_Scripts/UI/PlayerBuildStatusPanelUI.InventoryTabs.cs and meta
- Assets/02_Scripts/UI/PlayerBuildStatusPanelUI.cs
- Assets/02_Scripts/UI/PlayerBuildStatusPanelUI.StructuralFrames.cs
- Assets/02_Scripts/UI/StructuralFrameText.cs
- Assets/02_Scripts/UI/ExpeditionMenuController.cs
- Assets/02_Scripts/RunRuntime/RunLevelTraitSelectionUI.cs
- Assets/02_Scripts/RunRuntime/RunTraitChoiceButtonUI.cs
- Assets/02_Scripts/Data/ReinforcementDefinition.cs
- Assets/02_Scripts/Settlement/ShipTraitTreePanel.Equipment.cs
- Assets/02_Scripts/Settlement/SettlementController.cs

Validation:
- Assets/02_Scripts/UI/Editor/StructuralFrameAuthoring.cs (retain the new header/modal dimensions when rerun)
- Assets/02_Scripts/UI/Editor/Tests/InventoryPresentationTests.cs and meta
- Assets/02_Scripts/UI/Editor/Tests/SettlementAdditionalTraitsUIAuthoringTests.cs
- Assets/02_Scripts/UI/Editor/Tests/SettlementAdditionalTraitsUIAuthoringTests.Catalog.cs
- Tools/Validation/InventoryPresentationChecks.cs
- Tools/Validation/InventoryPresentationChecks.ps1
- Tools/Validation/InventoryAssetChecks.ps1

Documentation: SYSTEM_DESIGN.md, DESIGN_CHANGELOG.md, IMPLEMENTATION_STATUS.md and this report. Earlier structural conversion changes and the pre-existing solution deletion were preserved.

## 12. Validation results

| Check | Result |
|---|---|
| Runtime C# compilation using Unity's compiler/references | PASS: 0 errors, 52 existing warnings |
| Editor/test C# compilation | PASS: 0 errors, 4 existing warnings |
| Standalone checks executing production formatter/resolver code with thin engine adapters | PASS: 767 assertions; all 49 Trait / 25 Reinforcement mappings; plain/rich parity; palette; all frame combinations and penalty colors |
| Serialized/preservation audit against the current-tree baseline | PASS: 9,317 checks, including all existing IDs, hierarchy, binding, cargo-only manifest, key rectangles, Tutorial preservation and unchanged gameplay bodies |
| New NUnit suite | 33 focused parameterized cases authored and compiled; NOT executed |
| Unity EditMode run | BLOCKED before test execution; no XML results |
| Actual Play Mode / rendered screenshots | NOT performed / NOT captured |

Evidence is under Logs/InventoryValidation: RuntimeCompile.log, EditorCompile.log, UnityEditMode.log and the validation response/generated-source files. The Unity log records a refused LicenseClient-whdgk IPC connection and licensing not initialized; its task-owned stalled process was stopped. No unrelated process was stopped.

The asset preservation script uses this task's ignored Logs/InventoryPresentationBaseline snapshots. Standalone checks generate their adapters from the current production enums and formatters. Neither substitutes for Unity serialization or rendering tests. Git diff --check retains two pre-existing serialized blank-field whitespace findings in an untouched Expedition object; no new whitespace findings remain from this pass.

## 13. Actual 480 × 270 validation and screenshots

No rendered Play Mode result is claimed. The following remain required when Unity can initialize licensing:

- Tutorial and Expedition: Equipment with empty cargo, several normal modules, a frame module, a MAX card, Active charges/state, selected colored detail and scrolling.
- Cargo with Scrap/Core/Alloy at partial and near-full loads; auto-pickup toggle; 1 / Half / Max jettison.
- Cross-tab held-G/stale-selection checks, a valid equipment field drop, close/reopen and keyboard/controller focus.
- Reward choice, Settlement detail and fused-frame inspection using the same colors.
- Korean and English bounds at actual 480 × 270.

Requested screenshots — Equipment, Cargo, reward choice and Settlement detail — were not captured. There are no substitute/mock screenshots.

## 14. Remaining readability concerns

The saved layout reserves more space and scrolls variable text, but final TMP wrapping, font fallback, long English frame/resource labels, large quantities and controller navigation still require rendered verification. The expanded frame modal also needs the longest fused profile checked in both languages. No combat or frame balance concern was addressed in this presentation pass.

## 15. Protected scope

Enemy bases, bosses, combat balance, Trait values, manufacturing economy, equipment ownership/fitting, cargo rules, field-drop gameplay and Structural Frame balance were not changed by this pass. Existing story data remains intact. The post-ending Curse system was not implemented or changed. No commit or push was performed.
