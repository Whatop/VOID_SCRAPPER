# Route Core HUD and Hangar deployment presentation — completion report

Implemented from the current saved working tree on 2026-09-28. Existing local edits were preserved. No commit or push was made.

## Deck presentation

- The existing CombatStatus/HP/Armor/WeaponHeat hierarchy is now a 112×58 DarkUI card with a compact 96-pixel HP bar, numeric HP, a cyan Armor strip and a readable HEAT location. Existing PlayerHealth/PlayerArmor and weapon event subscriptions remain authoritative; zero maximum Armor still hides through the existing gauge rule.
- Only the Deck heat instance stays visible at zero for Machine Gun. Its optional area label is explicit, preventing GaugeBarUI from clearing it. Expedition's defaults remain unchanged. Shotgun and Sniper show no heat. Sniper charge retains its original player-relative follower and clears on release.
- The same objective text now sits in a 196×30 backing below the top rail. The interaction prompt stays separate at bottom-center. Purple Radar/hints retain their existing phase gating, positions and binding-aware text.
- ReturnToFacilities keeps the same Button and persistent LeaveDeck listener. The approved DarkUI Button_Small_A replaces the white oval appearance; normal is dark, hover/navigation focus cyan/blue and press/selection feedback yellow. Existing encounter code hides Return during defense and restores the prior active state.

## Hangar semantics

The previous body repeated static ShipDefinition HP/Cargo, zero movement/dash rows and empty passive text. The new body separates weapon/description/meaningful innate identity from a **fresh deployment projection, not runtime/max-level stats**.

Included, in runtime order:

1. Previewed ShipDefinition base values and the saved player movement/dash/Armor baseline. The existing inactive Deck applier is read only; tests verify its baselines match Expedition.
2. Effective compatible Shared and preview-ship branch fitting from PermanentProgress.AppendEffectiveEquipment using the preview ship's DefaultWeaponTree.
3. Exactly one fused StructuralFrameProfile resolved from effective frame IDs; individual frames are never summed.
4. Current Sector Technology levels and compatible owned persistent-story Lv1 effects applied by PlayerRuntimeStatApplier.
5. Ordinary fitted equipment's Lv1 effects only when RunRuntimeTraitStore.InitializeDeployment would seed them. HasRuntimePrerequisites equipment is excluded from the free Lv1 projection.

Excluded: existing run reward levels above Lv1, active reinforcement effects/charges, region modifiers, temporary statuses, depleted HP/Armor, portal carryover and boss temporary effects. Conditional protocols/weapon behaviours, active-cooldown and emergency-return-retention entries are omitted from this numeric presentation; the equipment screen continues to explain those behaviours. The preview starts no run, creates no hidden player, initializes no runtime store and mutates no PermanentProgress.

HP and Cargo always display. Armor displays only above zero. Other rows appear only for non-zero effective modifiers; no empty passive section appears. The helper calculates 25 runtime values: HP, Cargo, Armor, movement, dash distance/cooldown, damage, fire rate, projectile speed, range, spread, charge time/damage, harvest yield/object damage, recovery, scrap, pickup range, Radar radius/taunt/stealth, homing angle/range, projectile count and pierce count. Multiplicative effects remain multiplicative and numbers round to two decimals for display.

All colors come from StatPresentation.Rich: Health, Defense, Damage, FireRate, Movement, Dash, Cargo, Harvest (including scrap/object damage), ProjectileSpeed, Range, Accuracy, Charge, Radar, Recovery, Homing, Pierce and Special. Penalties keep their category color; signs convey direction. TMP rich text remains enabled.

Short bodies shrink the backing. Long bodies scroll; actual font measurement puts long translated entries on individual rows instead of overlapping two columns. Locked ships show weapon/description and analysis/development requirements, never fitted deployment values. Existing permanent Changed -> controller Changed -> HUD/navigation refresh handles fitting and technology/progression changes; language changes reuse that event path.

ShipDefinition's old swapped ResearchAccent caused the orange Sweeper title. ShipDefinition.WeaponAccent now supplies green Machine Gun, orange Shotgun and blue Sniper for research titles and Curse weapon accents. Purple Curse edges are unchanged. Equipment branch tabs use selection/hover colors, with no competing weapon-specific map. Stat colors remain separate.

The read-only current-save Sweeper capture projects **HP 33, Cargo 144, Armor 6, Damage +24.37%**. This is the current save snapshot, distinct from the disposable base/technology/frame stress cases.

## Verification

- Unity 6000.0.69f1 runtime/Editor compilation and saved authoring passed. Localization source was imported and validated through the existing Unity importer.
- **25/25 new tests passed.** Twelve runtime-equivalence cases cover no bonuses, technology only, Lightweight, Standard+Heavy, Shared Lv1, compatible branch, incompatible branch, effective fitted conditional equipment, all equipment on each of three ship branches, and a synthetic non-empty persistent-story effect. They compare all 25 runtime values against actual fresh PlayerRuntimeStatApplier plus RunRuntimeTraitStore/RunTraitEffectApplier fixtures. The final focused run additionally checks the three displayed chassis modifiers and proves the conditional fixture is effectively fitted before it is rejected from free Lv1.
- Final focused suite: **153/154 passed**. Full EditMode: **993/999 passed**. No new-test failure. See the baseline failures below; neither suite is claimed fully green.
- Actual Play Mode: **126 assertions**, **36 PNGs**, every image exactly **480×270**, **0 text-overflow findings**, **0 missing glyph findings**. Cases cover all requested Deck states and base/current/technology/three-ship/frame/many-modifier/locked Hangar views in Korean and English, including scrolled content.
- All **6,110 existing Settlement serialized IDs preserved**, 29 added, none removed. Encounter serialized gameplay and Return's persistent callback are byte-identical to the starting snapshot. Protected equipment/technology/ship assets, combat/shader sources and prefabs are unchanged. The original player save remains byte-identical. Owned source/document whitespace checks passed.

Evidence: [focused.xml](../../Logs/HudHangar/focused.xml), [full.xml](../../Logs/HudHangar/full.xml), [render audit](../../Logs/HudHangar/Rendered/audit.txt), [change audit](../../Logs/HudHangar/change-audit.txt), [starting-scene sprite audit](../../Logs/HudHangar/baseline-sprite-audit.txt).

## Existing failures and visual limits

- Three AnalyzedOldSavesDeriveUnlocksWithoutGrantReceiptOrCurrencyMutation cases (3/5/7) have the same failure messages as the earlier RouteCoreShader full-suite artifact.
- Three other full-suite failures originate from the existing unresolved SettlementHUD.cursedPreviewSprite reference: DarkUISettlementTests.ProductionRolesPreserveBindingsAndReauthorWithoutHierarchyOrStateDrift; PlayerChargeGaugePresentationTests.SavedFixedUiBindings_ValidateWithoutChangingPresentationOrProgress(Settlement); and SettlementStaticUIAuthoringTests.HangarIndicatorSelectionAndTweenCleanup_PreserveAuthoredBaselines. The latter cannot capture its Curse baseline after validation rejects the missing sprite. The untouched starting scene and final scene both reproduce the same missing reference; its GUID has no current AssetDatabase path. The serialized reference was preserved, not cleared or replaced.
- The prior square/placeholder Hangar preview silhouette remains; art replacement was not part of the stat/layout work. Surrounding legacy Settlement labels are still partly Korean in English captures. New Hangar title/body content is localized.
- Native captures were inspected. No standalone-player, alternative graphics API or complete campaign traversal was run in this pass. These remain outside the automated visual evidence.

## Changed files

- Scene: Assets/01_Scenes/Settlement.unity.
- Runtime presentation: Settlement/SettlementController.cs, SettlementHUD.cs, ShipDefinition.cs; new HangarDeploymentProjection.cs and HangarDeploymentText.cs; UI/WeaponHeatUI.cs. Player/PlayerRuntimeStatApplier.cs only gains read-only baseline properties.
- Editor and tests: new UI/Editor/HudHangarAuthoring.cs, HudHangarRenderProbe.cs, Tests/HudHangarLayoutTests.cs, Tests/SettlementAdditionalTraitsUIAuthoringTests.Hangar.cs, with Unity metadata.
- Localization: Config/Localization/Source/Localization.csv and importer-generated LocalizationCatalog.asset.
- Documentation: SYSTEM_DESIGN.md, DESIGN_CHANGELOG.md, IMPLEMENTATION_STATUS.md, ROUTE_CORE_DECK_AUTHORING.md and this report. Local QA scripts/evidence are under Logs/HudHangar.

**Explicitly unchanged:** equipment values/costs/fitting; Sector Technology values/costs; ShipDefinition gameplay stats; Route Core combat/Purple mechanics/shader; enemy/boss balance; campaign progression and save schema. No commit/push.

## Actual 480×270 screenshots

[Open all 36 native captures](../../Logs/HudHangar/screenshots.html).

| Requested coverage | Native capture |
|---|---|
| Normal inspection, Machine Gun idle, backed objective, Return normal | [Deck normal](../../Logs/HudHangar/Rendered/01-deck-normal-mg-idle-objective-return.png) |
| Return hover / focus / yellow press | [Hover](../../Logs/HudHangar/Rendered/02-return-hover.png), [focus](../../Logs/HudHangar/Rendered/03-return-focus.png), [press](../../Logs/HudHangar/Rendered/04-return-pressed-selection.png) |
| Machine Gun heat; HP/Armor | [Heat](../../Logs/HudHangar/Rendered/05-machinegun-heat.png), [HP 26 / Armor 6](../../Logs/HudHangar/Rendered/06-hp26-armor6.png) |
| Shotgun; Sniper charge/release | [Shotgun](../../Logs/HudHangar/Rendered/07-shotgun-no-heat.png), [charge](../../Logs/HudHangar/Rendered/08-sniper-charging.png), [release](../../Logs/HudHangar/Rendered/09-sniper-release.png) |
| Active-defense Return and Purple | [Defense](../../Logs/HudHangar/Rendered/10-active-defense-return-hidden.png), [Radar](../../Logs/HudHangar/Rendered/11-purple-radar-hint.png), [exposure](../../Logs/HudHangar/Rendered/12-purple-exposed.png) |
| Sweeper no bonuses | [Korean](../../Logs/HudHangar/Rendered/hangar-ko-base.png), [English](../../Logs/HudHangar/Rendered/hangar-en-base.png) |
| Sweeper current actual save | [Korean](../../Logs/HudHangar/Rendered/hangar-ko-current.png), [English](../../Logs/HudHangar/Rendered/hangar-en-current.png) |
| Sweeper equipment/technology fixture | [Korean](../../Logs/HudHangar/Rendered/hangar-ko-bonuses.png), [English](../../Logs/HudHangar/Rendered/hangar-en-bonuses.png) |
| Breacher | [Korean](../../Logs/HudHangar/Rendered/hangar-ko-breacher.png), [English](../../Logs/HudHangar/Rendered/hangar-en-breacher.png) |
| Lancer | [Korean](../../Logs/HudHangar/Rendered/hangar-ko-lancer.png), [English](../../Logs/HudHangar/Rendered/hangar-en-lancer.png) |
| Fused structural frame | [Korean](../../Logs/HudHangar/Rendered/hangar-ko-frame.png), [English](../../Logs/HudHangar/Rendered/hangar-en-frame.png) |
| Many modifiers and scroll | [Korean](../../Logs/HudHangar/Rendered/hangar-ko-many.png), [English](../../Logs/HudHangar/Rendered/hangar-en-many.png), [scrolled](../../Logs/HudHangar/Rendered/hangar-en-many-bottom.png) |
| Locked preview | [Korean](../../Logs/HudHangar/Rendered/hangar-ko-locked.png), [English](../../Logs/HudHangar/Rendered/hangar-en-locked.png) |
