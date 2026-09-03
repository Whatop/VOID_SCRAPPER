# VOID SCRAPPER 구현 상태 매트릭스

작성 기준일: 2026-08-30

이 표는 현재 저장소의 정적 근거를 기준으로 한다. 클래스 존재만으로 완료 판정하지 않았으며, 구현 수치는 별도 승인 근거가 없으면 테스트 값으로 취급한다. Unity Editor와 Play Mode 검증은 이번 감사 범위에 포함되지 않았다.

- Design Status는 Confirmed Design, Implementation, Testing, Planned, Deferred, Legacy 중 현재 문서화에 가장 중요한 판정을 사용한다.
- Implementation Status의 구현됨은 코드와 직렬화 연결이 확인됐다는 뜻이며, 플레이 검증 완료를 뜻하지 않는다.
- 부분 구현은 상태·데이터·코드 훅 중 일부만 있거나 전용 자산 연결이 빠진 경우다.

| System | Design Status | Implementation Status | Main Files/Assets | Needs Document Update | Notes |
|---|---|---|---|---|---|
| Core Loop | Confirmed Design | 구현됨 / 정적 검증 | Assets/02_Scripts/Core/Flow/RunManager.cs<br>Assets/02_Scripts/Core/Flow/RunContext.cs<br>Assets/02_Scripts/Expedition/ExpeditionBootstrap.cs | 예 — 확장 | 탐사 → 수확 → 전투 → 귀환 → 정착지 성장은 유지된다. 화물·작전·지역 계승 상태를 추가해야 한다. |
| Expedition Flow | Implementation | 구현됨 / 테스트 필요 | Assets/02_Scripts/RunRuntime/ExpeditionOperationController.cs<br>Assets/02_Scripts/Core/Flow/RunManager.cs<br>Assets/02_Scripts/Campaign/CampaignProgressionCatalog.cs | 예 — 전면 | 지역 1→2→3은 연속 런이며 최종 네트워크는 정착지에서 별도 출격한다. |
| Controls | Confirmed Design | 구현됨 / 직렬화 확인 | Assets/04_Input/PlayerControls.inputactions<br>Assets/02_Scripts/Player/PlayerController2D.cs<br>Assets/02_Scripts/Input/InputBindingUtility.cs | 예 — 전면 | 상호작용은 F다. E, Tab, R, G, Esc, Mouse 4가 추가됐으며 표시 키 하드코딩은 금지된다. |
| HUD | Confirmed Design | 구현됨 / 레이아웃 테스트 필요 | Assets/02_Scripts/UI/ExpeditionHUD.cs<br>Assets/01_Scenes/Expedition.unity<br>Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab | 예 — 전면 | XP 게이지 대신 Core Signal, Cargo, Reinforcement, 상태 효과, Alloy와 Tuning 표시가 중심이다. |
| Player | Testing | 부분 구현 / 데이터 점검 필요 | Assets/02_Scripts/Settlement/ShipDefinition.cs<br>Assets/02_Scripts/Settlement/01_basic_ship.asset<br>Assets/02_Scripts/Settlement/02_shotgun_ship.asset<br>Assets/02_Scripts/Settlement/03_sniper_ship.asset | 예 — 전면 | 세 함선 정의가 있으나 현재 HP가 모두 20이고 basic_ship / muchingun_ship ID 불일치가 있다. |
| Weapon Trees | Testing | 구현됨 / 콘텐츠·비용 테스트 필요 | Assets/02_Scripts/Settlement/ShipTraitTreePanel.cs<br>Assets/01_Scenes/Settlement.unity<br>Assets/02_Scripts/Config/Weapon_MachineGun.asset<br>Assets/02_Scripts/Config/Weapon_Shotgun.asset<br>Assets/02_Scripts/Config/Weapon_Sniper.asset | 예 — 전면 | 공용 및 3개 무기 분기가 있다. 함선 해금과 무기 선택의 최종 결합 규칙은 확정 근거가 부족하다. |
| Radar | Testing | 구현됨 / 규칙 테스트 필요 | Assets/02_Scripts/Temp/PlayerRadarScanner.cs<br>Assets/02_Scripts/UI/RadarTarget.cs<br>Assets/02_Scripts/Player/PlayerStealthController.cs | 예 — 전면 | Q 모드 전환, 활성 중 Mouse 4 즉시 스캔, 근거리 수동 탐지가 있다. Shotgun 도발과 무기별 경보는 대상 플래그에 따라 달라진다. |
| Map | Testing | 구현됨 / UX·수치 테스트 필요 | Assets/02_Scripts/Core/ExpeditionMapGenerator.cs<br>Assets/02_Scripts/Config/New Map Generation Config.asset<br>Assets/02_Scripts/UI/MapDiscoveryController.cs<br>Assets/02_Scripts/UI/ExpeditionRoutePlanner.cs | 예 — 신규 장 | 탐사 안개와 최대 5개 경유지를 가진 전체 지도가 있다. 지역 2는 상단 Core·스크롤 회랑을 예약하고, 지역 3은 Core·현장 기지·상점 없이 전용 보스와 반사판 8개를 생성한다. |
| Objectives / Operations | Testing | 구현됨 / 콘텐츠 테스트 필요 | Assets/02_Scripts/RunRuntime/ExpeditionOperationController.cs<br>Assets/02_Scripts/RunRuntime/ExpeditionObjectiveDirector.cs | 예 — 신규 장 | 지역당 작전 하나와 Core 공개·보스 보상에 쓰이는 Objective Signal 2/3/4 단계가 있다. |
| Cargo | Testing | 구현됨 / 밸런스 테스트 필요 | Assets/02_Scripts/Player/PlayerCargoController.cs<br>Assets/02_Scripts/Core/Flow/RunContext.cs<br>Assets/03_Prefabs/UI/PF_ExpeditionMapInventoryMenu.prefab | 예 — 신규 장 | Scrap·Core·Alloy에 용량·중량·과적·자동 회수·투기·긴급 귀환 보존 규칙이 적용된다. |
| Currency | Testing | 구현됨 / XP는 레거시 | Assets/02_Scripts/Economy/RunWallet.cs<br>Assets/02_Scripts/Core/Config/CoreTypes.cs<br>Assets/02_Scripts/Core/Flow/RunManager.cs<br>Assets/02_Scripts/Progression/PermanentProgress.cs | 예 — 전면 | Credits, Scrap, Core, Tuning Chips, Stabilized Alloy가 현재 경제다. Experience는 새 보상 경로가 없다. |
| Trait | Testing | 구현됨 / 콘텐츠 테스트 필요 | Assets/02_Scripts/Data/TraitDefinition.cs<br>Assets/02_Scripts/Config/Catalog/TraitCatalog_Main.asset<br>Assets/02_Scripts/RunRuntime/RunTraitAcquisitionService.cs | 예 — 전면 | 획득원 기반 런 Trait, 중복 강화, 교체·분해, 보스 Trait, Tuning 강화가 있다. XP 레벨업 선택은 현행 경로가 아니다. |
| Pixel Curse | Confirmed Design | 구현됨 / 연출 테스트 필요 | Assets/02_Scripts/Config/TraitDefinition/Story/45_pixel_curse.asset<br>Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Player/PlayerVisualStateController.cs | 예 — 신규 장 | 지속·비드롭·비분해 서사 Trait이며 튜토리얼 감염 경로가 있다. |
| Reinforcement | Testing | 구현됨 / 41개 콘텐츠 테스트 필요 | Assets/02_Scripts/Player/PlayerReinforcementController.cs<br>Assets/02_Scripts/Data/ReinforcementDefinition.cs<br>Assets/02_Scripts/Config/Catalog/Reinforcement Catalog.asset | 예 — 신규 장 | R 장착 사용, 충전·재충전, 무기 제한, 교체·분해, 생성물 소유권 경로가 있다. 컨트롤러 권위 구조는 안정적이다. |
| Harvest | Testing | 구현됨 / 보상 테스트 필요 | Assets/02_Scripts/HarvestObjectHealth.cs<br>Assets/02_Scripts/Enemies/RewardDropper.cs<br>Assets/02_Scripts/Config/Container.asset<br>Assets/02_Scripts/Config/HighValueWreck.asset<br>Assets/02_Scripts/Config/DestroyedHull.asset | 예 — 부분 | 세 수확 유형은 유지되며 Alloy, Trait, Reinforcement와 작전 연계가 추가됐다. |
| Enemy Combat | Testing | 구현됨 / 수치 테스트 필요 | Assets/02_Scripts/Enemies/EnemyBaseAI.cs<br>Assets/02_Scripts/Enemies/EnemyState.cs<br>Assets/02_Scripts/Data/EnemyDefinition.cs<br>Assets/02_Scripts/Config/EnemyDefinition/ | 예 — 부분 | 기존 FSM은 유지되며 공격 패턴, 감지, Elite 변형과 지역 스케일이 확장됐다. 현재 HP 수치는 프로토타입과 다르다. |
| Enemy Roles | Confirmed Design | 구현됨 / 정적 검증 | Assets/02_Scripts/Enemies/EnemyRoleController.cs<br>Assets/02_Scripts/Enemies/EnemyRoleSimulationGate.cs<br>Assets/02_Scripts/Core/ExpeditionMapGenerator.cs | 예 — 신규 장 | Defender, RivalHarvester, Scavenger와 원거리 시뮬레이션 단계가 있다. 임시 유인·감속은 출처별 상태를 보존한다. |
| Events | Testing | 구현됨 / 보상·난이도 테스트 필요 | Assets/02_Scripts/RunRuntime/ExpeditionEventObject.cs<br>Assets/03_Prefabs/Event/<br>Assets/02_Scripts/Config/EventRewards/ | 예 — 전면 | Rescue, Unknown Device, Unstable Reactor, Black Box 네 유형이 있고 Objective Signal과 연결된다. |
| Shop | Testing | 구현됨 / 경제·전투 테스트 필요 | Assets/02_Scripts/Shop/ShopStructure.cs<br>Assets/02_Scripts/ShopStockController.cs<br>Assets/02_Scripts/Shop/ShopTradeUI.cs<br>Assets/03_Prefabs/Enemy/PF_ShopStructure.prefab | 예 — 전면 | 중립·경고·전역 적대는 유지된다. 무작위 Trait·Reinforcement, 안전 구역, 보안망, Maintenance Bay가 추가됐다. |
| Core | Testing | 구현됨 / 최종 보스 연결 누락 | Assets/02_Scripts/Core/CoreObject.cs<br>Assets/03_Prefabs/Object/Core.prefab<br>Assets/02_Scripts/RunRuntime/ExpeditionObjectiveDirector.cs<br>Assets/02_Scripts/Core/ExpeditionMapGenerator.cs | 예 — 전면 | 지역 1·2 Core는 Signal 2개 전까지 숨겨지고 2초 활성화를 사용한다. 지역 2 전용 보스가 연결됐으며 Core 보상은 사망 시점으로 미뤄진다. 지역 3은 Core 없는 전용 조우이고 최종 보스 참조는 여전히 없다. |
| Region 1 | Testing | 구현됨 / 플레이 검증 필요 | Assets/03_Prefabs/Enemy/Boss.prefab<br>Assets/02_Scripts/Boss/BossPatternController.cs<br>Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Region1.asset<br>Assets/03_Prefabs/Enemy/PF_Boss_RaiderCommander.prefab | 예 — 전면 | Sector Administrator와 재도전 Raider Commander가 있다. 현재 Phase 2 임계값은 프로토타입 30%와 다른 50%다. |
| Region 2 | Testing | 전용 조우 연결됨 / Play Mode 검증 필요 | Assets/03_Prefabs/Enemy/PF_Boss_SalvageDevourer_FrigateTriad.prefab<br>Assets/02_Scripts/Boss/FrigateTriadBossController.cs<br>Assets/02_Scripts/Boss/SalvageDevourerCorridorController.cs<br>Assets/03_Prefabs/Object/PF_Region2BossCorridorRuntime.prefab<br>Assets/03_Prefabs/Object/Core.prefab | 예 — 신규 장 | 상단 Core, 세로 스크롤 회랑, 3기 편대의 생존 수별 패턴, 마지막 돌진, 사망 시 Core 보장 지급이 직렬화 연결돼 있다. 수치와 실행 감각은 테스트 상태다. |
| Region 3 | Testing | 전용 조우 연결됨 / Core 보상 경로 미확인 | Assets/03_Prefabs/Enemy/PF_Boss_PhaseGatekeeper.prefab<br>Assets/03_Prefabs/Object/PF_Region3_PhaseReflectorPlate.prefab<br>Assets/02_Scripts/Boss/PhaseGatekeeperBossController.cs<br>Assets/02_Scripts/Boss/PhaseReflectorPlate.cs<br>Assets/01_Scenes/Expedition.unity | 예 — 신규 장 | Core 없는 맵에 보스와 반사판 8개가 생성된다. 은폐·반사 레이저 3회·노출 피해 창은 연결됐지만, 캠페인 정의의 Core 2개 지급 호출은 정적 검색으로 확인되지 않았다. |
| Boss Rewards | Testing | 구현됨 / 지역 3 Core 연결 점검 필요 | Assets/02_Scripts/RunRuntime/BossRewardExitCoordinator.cs<br>Assets/02_Scripts/Core/BossDummyController.cs<br>Assets/02_Scripts/Campaign/CampaignBossRewardService.cs<br>Assets/02_Scripts/Data/BossCampaignDefinition.cs | 예 — 전면 | 고정 캠페인 보상과 3개 선택 보상이 분리된다. 지역 2 Core는 사망 시 보장·중복 방지 처리된다. Signal 3/4 보너스가 있으며 지역 3 Core 지급은 미확인이다. |
| Return | Testing | 구현됨 / 정산 테스트 필요 | Assets/02_Scripts/EmergencyReturnController.cs<br>Assets/02_Scripts/Core/ReturnBeacon.cs<br>Assets/02_Scripts/Core/WormholePortal.cs<br>Assets/02_Scripts/Core/Flow/RunManager.cs | 예 — 전면 | 긴급 귀환은 기본 화물 용량 70% 보존이며 고정 20% 손실이 아니다. 사망·안전 귀환·지역 이동 정산이 각각 다르다. |
| Campaign Progression | Planned | 부분 구현 / 정착지 경로 미연결 | Assets/02_Scripts/Progression/PermanentProgress.cs<br>Assets/02_Scripts/Campaign/CampaignProgressionCatalog.cs<br>Assets/02_Scripts/Campaign/SettlementRouteCoreController.cs | 예 — 신규 장 | 세 보스 부품, Route Core, 정착지 방어, 최종 출격 상태는 있으나 Route Core 컨트롤러가 씬·프리팹에 없다. |
| Settlement | Testing | 부분 구현 / 콘텐츠·연결 점검 필요 | Assets/02_Scripts/Settlement/SettlementController.cs<br>Assets/02_Scripts/Settlement/ShipTraitTreePanel.cs<br>Assets/02_Scripts/Settlement/SectorTechnologyCatalog.cs<br>Assets/01_Scenes/Settlement.unity | 예 — 전면 | 건물 3단계 업그레이드는 레거시다. 현재는 이진 복구, 함선·영구 Trait Tree, Alloy 기술 성장이다. |
| Tutorial | Testing | 구현됨 / Play Mode 검증 필요 | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Tutorial/TutorialStep.cs<br>Assets/01_Scenes/Tutorial.unity | 예 — 신규 장 | 28단계가 운영 시스템과 실제 긴급 귀환을 사용한다. 이번 감사에서는 실행 검증하지 않았다. |
| Story | Planned | 부분 구현 | Assets/02_Scripts/Config/Dialogue/VOID_SCRAPPER_DialogueDatabase.asset<br>Assets/02_Scripts/Dialogue/DialogueStoryEntryPoint.cs<br>Assets/02_Scripts/RunRuntime/FieldNpcObjective.cs | 예 — 전면 | 튜토리얼·첫 정착지·NPC 서비스 대화는 연결된다. 지역 보스 서사와 후반 캠페인 대화는 부족하다. |
| Final Boss / Ending | Planned | 시작 불가 / 전용 자산 누락 | Assets/02_Scripts/Resources/Campaign/BossDefinitions/BossCampaign_Final.asset<br>Assets/02_Scripts/Core/CoreObject.cs<br>Assets/02_Scripts/Campaign/FinalBossSettlementSupportPhase.cs<br>Assets/03_Prefabs/Object/Core.prefab | 예 — 계획 장만 | Null Dispatcher 상태와 FinalVictory 훅은 있으나 finalBossPrefab 연결, 전용 전투, 엔딩 시퀀스가 없다. |

## 문서화 우선순위

1. 새 시스템 설계서에 바로 옮길 수 있는 구조: 핵심 루프, 입력 표시 규칙, HUD 정보 우선순위, PlayerReinforcementController 권위, Enemy 역할과 임시 효과 소유권, Pixel Curse 영구 규칙.
2. 구현을 기준으로 쓰되 테스트 표기가 필요한 구조: Radar·Map, Cargo·Currency, Trait, Harvest, Events, Shop, Core, 지역 1~3 보스, 보상·귀환, Settlement 성장 수치.
3. 계획으로만 써야 하는 구조: Route Core 기반 정착지 방어, 최종 보스, 엔딩과 후반 Story.
4. 레거시로 분리할 항목: 월드 래핑, XP 런 레벨업, 건물별 3단계 업그레이드, 사용되지 않는 개발용 대화 경로.

## Dialogue and Localization Phase 2A — 2026-08-31

Status vocabulary for this section is explicit: `Not Started`, `Implemented`,
`Static Verified`, and `Play Mode Verified`. Multiple values mean the implementation
exists and has reached the listed verification level.

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| SYSTEM_DESIGN.md | Implemented / Static Verified | Docs/Design/SYSTEM_DESIGN.md | Authoritative dialogue/localization boundary created. |
| Localization catalog | Implemented / Static Verified | Assets/02_Scripts/Localization/LocalizationCatalog.cs<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | Generated immutable runtime lookup with Korean fallback metadata. |
| Localization service | Implemented / Static Verified | Assets/02_Scripts/Localization/VoidScrapperLocalizationService.cs<br>Assets/03_Prefabs/UI/Dialogue/PF_DialogueManager.prefab | Project-owned service serialized on the existing persistent Dialogue Manager. Play Mode verification pending. |
| Language settings integration | Implemented / Static Verified | Assets/02_Scripts/Settings/GameSettingsRuntime.cs | Device-local PlayerPrefs language; no SaveData change. |
| Localized TMP presenter | Implemented / Static Verified | Assets/02_Scripts/Localization/LocalizedTextPresenter.cs | Event-driven; production UI bulk migration not started. |
| Localization CSV | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/Source/Localization.csv | Korean-only foundation rows; no invented production translations. |
| Localization importer | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/LocalizationContentImporter.cs | Editor-only UTF-8 import; runtime CSV loading absent. |
| Localization validator | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/LocalizationContentValidator.cs<br>Assets/02_Scripts/Localization/Editor/LocalizationCsvParser.cs | Validates keys, Korean source, placeholders, CSV, metadata, references, and freshness. |
| Automated tests | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/Tests/LocalizationFoundationTests.cs | Test assembly compiles; Unity EditMode execution pending. |
| Pixel Crushers language forwarding | Implemented / Static Verified | Assets/02_Scripts/Localization/VoidScrapperLocalizationService.cs | Uses supported language API; Korean maps to default/source language. |
| Runtime language refresh | Implemented / Static Verified | Assets/02_Scripts/Localization/VoidScrapperLocalizationService.cs<br>Assets/02_Scripts/Localization/LocalizedTextPresenter.cs | Instance event, no polling; Play Mode verification pending. |
| CJK font verification | Not Started | Assets/07_Txt/<br>Assets/TextMesh Pro/Resources/TMP Settings.asset | Requires Unity glyph and fallback-font checks. |
| Full UI migration | Not Started | Existing UI and data definitions | Explicitly outside Phase 2A. |
| Dialogue action system | Not Started | Deferred to Phase 2B | Existing gameplay owners must remain authoritative. |
| Dialogue condition system | Not Started | Deferred to Phase 2B | No Phase 2A condition runtime added. |
| RescueContact vertical slice | Not Started | Deferred to Phase 2B | `FieldNpcObjective` behavior remains unchanged. |

## Dialogue and Localization Phase 2B — 2026-09-01

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| RescueContact localization keys | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/Source/Localization.csv<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | Korean source and fallback; optional translations intentionally empty. |
| Dialogue condition registry | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueConditionRegistry.cs | Only `RescueContactAvailable`; read-only and fail-closed. |
| Dialogue gameplay action dispatcher | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueGameplayActionDispatcher.cs | Only `RescueContactAccept`; deduplicates within one conversation and calls existing authority. |
| Pixel Crushers Phase 2B bridge | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialoguePixelCrushersBridge.cs<br>Assets/03_Prefabs/UI/Dialogue/PF_DialogueManager.prefab | Uses verified Lua registration and controller lifecycle APIs; Play Mode pending. |
| RescueContact service authority adapter | Implemented / Static Verified | Assets/02_Scripts/RunRuntime/FieldNpcObjective.cs<br>Assets/03_Prefabs/NPC/NPC_RescueContact.prefab | Dialogue requests the existing service; objective signal remains owned by ExpeditionObjectiveDirector/RunContext. |
| RescueContact conversation installer | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/RescueContactDialogueInstaller.cs | Validates in memory and replaces only the named conversation; must be run in Unity. |
| RescueContact Dialogue Database wiring | Not Started | Assets/02_Scripts/Config/Dialogue/VOID_SCRAPPER_DialogueDatabase.asset | Manual Editor installer execution pending; asset was not rewritten outside Unity. |
| RescueContact EditMode tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/RescueContactDialogueTests.cs | Test source compiles; Unity Test Runner execution pending. |
| Full dialogue action catalog | Not Started | Future migrations | Explicit registration remains required per gameplay owner. |
| Full dialogue condition catalog | Not Started | Future migrations | Only the RescueContact availability condition exists. |
| Recommended next migration | Not Started | NPC_FieldTechnician_Service | Migrate only after RescueContact Play Mode authority and lifecycle verification. |

## Dialogue Stability Phase 2B.1 — 2026-09-01

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Canonical persistent Dialogue Manager owner | Implemented / Static Verified | Assets/02_Scripts/Core/Bootstrap/GameBootstrap.cs<br>Assets/01_Scenes/Boot.unity<br>Assets/03_Prefabs/UI/Dialogue/PF_DialogueManager.prefab | Boot `CoreRoot` is creation authority; repeated scene-loop Play Mode verification pending. |
| Incoming Boot duplicate pruning | Implemented / Static Verified | Assets/02_Scripts/Core/Bootstrap/GameBootstrap.cs | Incoming scene manager root is deactivated and removed before its default execution-order `Awake`; defensive service guard remains. |
| Settlement authoritative transition guard | Implemented / Static Verified | Assets/02_Scripts/Settlement/SettlementExpeditionLaunchGuard.cs<br>Assets/02_Scripts/Settlement/SettlementController.cs | Active conversation fails closed with a typed reason; existing ship/run requirements remain authoritative. |
| Settlement modal UI input | Implemented / Static Verified | Assets/02_Scripts/Settlement/SettlementUIController.cs | Event-driven CanvasGroup and keyboard gating; Pixel Crushers response UI remains interactive. |
| Settlement dialogue-block localization | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/Source/Localization.csv<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | Korean source/fallback; optional translations intentionally empty. |
| Phase 2B.1 automated tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/DialogueLifecycleStabilityTests.cs | Runtime and Editor test assemblies compile; Unity Test Runner execution pending. |
| MainMenu/Boot/Settlement loop | Not Started | Unity Play Mode | Repeat three times; exactly one manager/service/bridge required. |
| Settlement pointer/keyboard/programmatic modal test | Not Started | Unity Play Mode | Must verify all launch paths block during mandatory dialogue and recover after it ends. |
| Phase 2C story migration | Implemented / Static Verified | Tutorial -> first Settlement story -> main quest | Code, content, and generated-data validation are complete; the Phase 2B.1 lifecycle gate and Phase 2C flow still require Play Mode verification. |

## Dialogue Story Phase 2C — 2026-09-01

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Phase 2C authority decision | Implemented / Static Verified | Docs/Design/SYSTEM_DESIGN.md | Pixel Crushers owns graphs; tutorial, campaign progress, rewards, and saving remain in existing owners. |
| Tutorial story guidance | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Editor/Phase2CStoryDialogueInstaller.cs | Four one-line graphs run at Move, Radar, supply-container, and detected ancient-signal stages; natural completion is exact-once and interrupted stages remain pending. Unity installation/Play Mode pending. |
| Unknown access-key contact | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Editor/Phase2CStoryDialogueInstaller.cs | Natural completion advances the existing Pixel Curse checkpoint; no direct Trait/visual/save mutation. |
| Post-Curse rescue integration | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Natural completion advances the existing rescue/return authority; interruption leaves the checkpoint pending. |
| First Settlement analysis | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueStoryEntryPoint.cs<br>Assets/01_Scenes/Settlement.unity | Pending/complete flags and a natural terminal marker gate completion; Play Mode pending. |
| Main quest typed conditions | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueConditionRegistry.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Read-only `NotStarted`, 0/3, 1/3, 2/3, ready, and completed projections; unknown/missing authorities fail closed. |
| Main quest exact-once start | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueGameplayActionDispatcher.cs<br>Assets/02_Scripts/Settlement/SettlementController.cs<br>Assets/02_Scripts/Progression/PermanentProgress.cs | Settlement atomically starts when needed, records story completion, saves, and notifies after successful conversation completion; existing unlock flags supply recovery and persistent idempotency. |
| Boss-part progress binding | Implemented / Static Verified | Assets/02_Scripts/Progression/PermanentProgress.cs<br>Assets/02_Scripts/Core/Flow/RunManager.cs | Read-only quest state derives from the existing three campaign-boss part grants; no dialogue reward write added. Play Mode pending. |
| Phase 2C localization | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/Source/Localization.csv<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | Korean source/fallback for seven graphs, speakers, quest start/progress/restoration, Curse level notification, and named placeholders; strict CSV/catalog validation passes. |
| Deterministic graph installer/validator | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Phase2CStoryDialogueInstaller.cs | Installs seven named graphs and one stable Operator actor through Pixel Crushers APIs; source compiles and cloned-database tests exist, but production Dialogue Database execution remains pending in Unity. |
| Mandatory Settlement launch lock | Implemented / Static Verified | Assets/02_Scripts/Settlement/SettlementExpeditionLaunchGuard.cs<br>Assets/02_Scripts/Settlement/SettlementController.cs | Pending story blocks pointer/keyboard/programmatic launch even before conversation starts; Play Mode pending. |
| Phase 2C EditMode tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Covers state reads, exact-once action, interruption, save identity, graph determinism, localization, and launch gating. Test source compiles; Unity Test Runner execution pending. |
| Dialogue Database Phase 2C wiring | Not Started | Assets/02_Scripts/Config/Dialogue/VOID_SCRAPPER_DialogueDatabase.asset | Run the project Editor installer; no direct YAML edit was made in Phase 2C. |
| Main quest HUD binding | Not Started | Future approved HUD surface | Localization key exists; no generic authoritative main-quest presenter was found, so Phase 2C does not invent one. |
| Phase 2C Play Mode verification | Not Started | Tutorial / Settlement / Expedition | Requires fresh tutorial, interruption/re-entry, exact-once start, all repeat states, save/load, scene loop, RescueContact, pause, and fallback checks. |

## Dialogue Story Phase 2C.1 — 2026-09-01

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Staged tutorial pacing | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Guidance state is event-driven and runtime-local; gameplay objectives retain their existing authority. |
| Quest natural-start boundary | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueStoryEntryPoint.cs<br>Assets/02_Scripts/Settlement/SettlementController.cs | Existing pending flag and natural terminal action start the quest once; Settlement load alone performs no start. |
| Ready-to-restore progression | Implemented / Static Verified | Assets/02_Scripts/Progression/PermanentProgress.cs | Three distinct boss parts project `ReadyToRestore`; no expedition-side automatic completion was added. |
| Settlement Recovery integration | Implemented / Static Verified | Assets/02_Scripts/Settlement/SettlementController.cs<br>Assets/02_Scripts/Settlement/SettlementUIController.cs | Recovery Processor temporarily presents explicit access-key restoration; success changes authoritative RouteCore state and saves once. Play Mode pending. |
| Pixel Curse levels | Implemented / Static Verified | Assets/02_Scripts/Progression/PermanentProgress.cs<br>Assets/02_Scripts/Core/Flow/RunManager.cs | Level is derived as inactive 0 or `min(1 + distinct parts, 4)`; duplicate parts emit no level change. No new combat effects. |
| Phase 2C.1 localization/catalog | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/Source/Localization.csv<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | 44 records, deterministic hash, Korean fallback, and `{level}` / quest placeholders validate. |
| Phase 2C.1 EditMode tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Test source compiles; Unity Test Runner execution remains pending because an Editor instance is open. |
| Phase 2C.1 Play Mode | Not Started | Tutorial / Settlement / Expedition | Verify staged checkpoints, interruption, recovery confirmation, Curse notifications, 480x270 wrapping, save reload, and no duplicate-service error during normal scene transitions. |

## Dialogue Story Phase 2C.2 — 2026-09-02

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| High-value wreck camera presentation | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Core/GungeonStyleCamera2D.cs | Map/Radar discovery acquires owner-scoped camera and input locks, blends to the wreck for 0.6s, starts dialogue after focus, then returns for 0.5s before exact-once advancement. Both blends use unscaled time. |
| High-value interruption cleanup | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Duplicate discovery is rejected. Conversation interruption, invalid target, camera takeover, RunEnded, disable, and destroy release only tutorial-owned state; interrupted guidance remains replayable. |
| Optional supply route | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Route placement or physical camera/proximity approach enters the same supply sequence. Existing `HarvestObjectHealth.Died` is the final box-completion event. |
| Phase 2C.2 EditMode tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Added presentation-phase, duplicate/interruption, owner/unscaled integration, optional-route, and exact-once convergence coverage. Unity Test Runner execution pending. |
| Phase 2C.2 Play Mode | Not Started | Tutorial | Verify offscreen focus, paused dialogue hold, natural return, interruption/scene cleanup, optional-route play, and 480x270 framing. |

## Dialogue Story Phase 2C.3 — 2026-09-02

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Radar mode toggle | Implemented / Static Verified | Assets/02_Scripts/Temp/PlayerRadarScanner.cs<br>Assets/04_Input/PlayerControls.inputactions | Q toggles the sole active state and never scans. Existing binding was already `<Keyboard>/q`. |
| Active-mode instant scan | Implemented / Static Verified | Assets/02_Scripts/Temp/PlayerRadarScanner.cs<br>Assets/04_Input/PlayerControls.inputactions | Mouse 4 uses the existing `<Mouse>/backButton` action, press-only input, and existing 0.5s cooldown. Inactive/locked requests fail closed. |
| Radar charge retirement | Implemented / Static Verified | Assets/02_Scripts/Temp/PlayerRadarScanner.cs<br>Assets/02_Scripts/UI/PlayerRadarVFXController.cs<br>Assets/02_Scripts/Core/Config/GameBalanceConfig.cs | Radar hold/release/cancel state and Radar-only gauge/effect dependency are removed. Shared weapon charge UI is unchanged. |
| Radar tutorial signals | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Tracks Radar activation and successful authoritative supply-target scan separately; Q alone cannot advance. |
| Shared supply/wreck presentation | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Core/GungeonStyleCamera2D.cs | Both targets use one owner-scoped, unscaled focus/dialogue/return pipeline with typed target identity and idempotent cleanup. |
| Input/control copy | Implemented / Static Verified | Assets/02_Scripts/UI/SharedOptionsMenuUI.cs<br>Assets/02_Scripts/Config/Localization/Source/Localization.csv | Control labels identify Radar toggle and instant scan; tutorial dialogue/objective resolve current bindings. |
| Tutorial newline validation | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/LocalizationContentValidator.cs | Embedded newlines in `dialogue.tutorial.*` language cells fail validation; normal RFC 4180 multiline support remains available elsewhere. |
| Phase 2C.3 automated tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs<br>Assets/02_Scripts/Localization/Editor/Tests/LocalizationFoundationTests.cs | Runtime and Editor assemblies compile; Unity Test Runner execution pending while the Editor is open. |
| Phase 2C.3 Play Mode | Not Started | Tutorial / Expedition | Verify Q/Mouse 4 input, lock behavior, both camera presentations, interruptions, optional route, 480x270 wrapping, and no normal-runtime duplicate localization service error. |

## Dialogue Presentation Phase 2C.4 — 2026-09-02

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Korean-safe resolved wrapping | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueWordWrapUtility.cs<br>Assets/02_Scripts/Dialogue/DialogueSubtitleTypewriter.cs | TMP word joiners protect whitespace-delimited tokens after localization/variable resolution; source content remains unmodified. |
| Stable TMP typewriter | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueSubtitleTypewriter.cs<br>Assets/03_Prefabs/UI/Dialogue/PF_CommunicationDialogueUI.prefab | Full text is laid out before `maxVisibleCharacters` reveal. Existing Pixel Crushers continue/pause ownership remains. |
| Configurable punctuation rhythm | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueSubtitleTypewriter.cs | Uses unscaled 0.03 glyph, 0.08 comma, 0.18 sentence, and 0.28 ellipsis timing. Play Mode feel verification pending. |
| Operator text voice | Implemented / Static Verified | Assets/06_Audio/SFX/Talk/test_talk-sfx.wav<br>Assets/03_Prefabs/UI/Dialogue/PF_CommunicationDialogueUI.prefab | One reusable 2D source, UI mixer routing, every-two-glyph cadence, pitch 0.97-1.03. Volume/fatigue verification pending. |
| Dialogue presentation validation | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/DialoguePresentationValidator.cs<br>Assets/02_Scripts/Localization/Editor/LocalizationContentImporter.cs | Checks two-line reference layout, oversized tokens, generated wrapping characters in source, unbalanced rich text, unresolved bindings, and existing newline rules. Unity execution pending. |
| Phase 2C.4 EditMode tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/DialoguePresentationTests.cs | Source compiles and covers wrapping, binding particles, rich text/placeholders, timing/cadence policy, prefab wiring, and reference layout. Unity Test Runner pending. |
| Phase 2C.4 Play Mode | Not Started | Tutorial dialogue at 480x270 | Verify four staged lines, Korean fallback, blip mix/fatigue, fast-forward, interruption, and source cleanup. |

## Cinematic Dialogue Presentation Phase 2C.5 — 2026-09-02

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Typed presentation policy | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialoguePresentationPolicy.cs | Stable conversation/actor IDs select CompactGuidance, ContextFocus, CinematicCommunication, and actor theme; no localized-name matching or camera ownership. |
| Communication UI presentation | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueCinematicPresentationController.cs<br>Assets/03_Prefabs/UI/Dialogue/PF_CommunicationDialogueUI.prefab | Reuses Pixel Crushers panels and selectable continue controls; adds 0/14/40% dim profiles, 14px top bar, 60/72px lower panel, 44px portrait area, and unscaled transitions. |
| Actor themes and text voices | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueSubtitleTypewriter.cs<br>Assets/06_Audio/SFX/Talk/ | Operator cyan, Curse purple, Settlement warm, unknown neutral/silent. One reusable AudioSource selects existing Talk clips. |
| HUD/camera ownership boundary | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueCinematicPresentationController.cs<br>Assets/02_Scripts/UI/ExpeditionHUD.cs | Cinematic mode uses owner-scoped HUD suppression. ContextFocus does not acquire or release camera/input ownership. |
| Interruption cleanup | Implemented / Static Verified | Assets/02_Scripts/Dialogue/DialogueCinematicPresentationController.cs | Completion, stop-all, scene unload, disable, and destroy converge on idempotent unscaled cleanup and selection restoration. |
| Phase 2C.5 validation/tests | Implemented / Static Verified | Assets/02_Scripts/Localization/Editor/DialoguePresentationValidator.cs<br>Assets/02_Scripts/Dialogue/Editor/Tests/DialoguePresentationTests.cs | Policy, actor mapping, duplicate lifecycle, prefab layout/timing/audio, selectable continue, and existing 480x270 two-line validation compile. Unity Test Runner pending. |
| Phase 2C.5 Play Mode | Not Started | Tutorial / Settlement at 480x270 | Verify mode transitions, HUD restoration, target framing order, controller/mouse/keyboard continue, actor mix, interruption, scene loops, and missing-actor fallback. |

## QA Stabilization Pass 1 — 2026-09-02

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Post-boss exit placement | Implemented / Static Verified | Assets/02_Scripts/RunRuntime/BossRewardExitCoordinator.cs | Exact-once deterministic pair is bounded to an authoritative combat/map area and avoids Player, reward/death point, blockers, and pair overlap. Play Mode pending. |
| Region-2 laser ownership | Implemented / Static Verified | Assets/02_Scripts/Boss/FrigateTriadBossController.cs<br>Assets/02_Scripts/Boss/BossLaserHazard.cs | Damaging beam follows the moving boss root; pooled geometry, collider, renderer, and active state reset explicitly. |
| Composite homing aim | Implemented / Static Verified | Assets/02_Scripts/Boss/FrigateBossPart.cs<br>Assets/02_Scripts/Player/Bullet.cs | Uses the active damageable collider's closest point and retains existing validity, faction, lifetime, and turn rules. |
| Boss entrance presentation | Implemented / Static Verified | Assets/02_Scripts/Temp/CoreBossIntroSequence.cs<br>Assets/02_Scripts/Core/GungeonStyleCamera2D.cs | Region 1 retains offscreen staging; Region 2 blends into the corridor before scroll handoff. Presentation timing is unscaled and cleanup remains owner-scoped. |
| Charged enemy aim commitment | Existing / Static Verified | Assets/02_Scripts/Enemies/EnemyAttackController.cs | Early prediction and final committed direction already drive the same warning and projectile; no production change required. |
| Radar persistence/presentation | Implemented / Static Verified | Assets/02_Scripts/Temp/PlayerRadarScanner.cs<br>Assets/02_Scripts/UI/RadarHUD.cs<br>Assets/02_Scripts/UI/PlayerRadarVFXController.cs | Scan remains open through ordinary combat; manual Q and lifecycle shutdown close it, while explicit boss-owned suppression remains. Background uses 55% alpha while markers remain full contrast; pulse is cyan. |
| Unknown objective/guidance | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/UI/RadarMarkerUI.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | One `?` marker follows objective lifecycle. Stable localization keys select open-plus-scan or scan-only detail without restarting dialogue. |
| Tuning Chip HUD | Implemented / Static Verified | Assets/02_Scripts/UI/ExpeditionHUD.cs | Reuses currency counter, wallet event, zero-value policy, and gapless repack; 480x270 Play Mode pending. |
| Settlement Use visibility | Implemented / Static Verified | Assets/02_Scripts/Settlement/ShipTraitTreePanel.cs | Hidden before unlock; existing activate/deactivate authority is retained after unlock. |
| Shop purchase SFX | Implemented / Static Verified | Assets/02_Scripts/Shop/ShopTradeUI.cs<br>Assets/02_Scripts/Audio/SoundEventIds.cs<br>Assets/06_Audio/Resources/Audio/SoundEventLibrary.asset<br>Assets/06_Audio/SFX/Shop/shop_buy_success.wav | Existing success clip is assigned to the existing Sound Event and invoked only after confirmed purchase. Play Mode mix verification pending. |
| QA Pass 1 EditMode tests | Implemented / Not Run | Assets/02_Scripts/Dialogue/Editor/Tests/QAStabilizationPass1Tests.cs | Unity-generated Editor project/test assembly refresh and Test Runner execution pending while Unity owns the project. |
| QA Pass 1 Play Mode | Not Started | Region 1 / Region 2 / Tutorial / Settlement | Verify boss exits, moving Frigate beam, homing, entrances, telegraph, Radar combat close, marker cleanup, HUD layout, unlock refresh, and success-only shop audio. |

## Tutorial QA Presentation Pass 2 — 2026-09-03

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Movement-gated Operator opening | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | `PlayerController2D.MovementStarted` starts one pending transmission; natural Pixel Crushers completion remains required and the 2.75-unit movement objective is unchanged. Approved incoming cue asset/event assignment remains pending. |
| Restrained Radar pulse | Implemented / Static Verified | Assets/02_Scripts/UI/PlayerRadarVFXController.cs<br>Assets/02_Scripts/Temp/PlayerRadarScanner.cs | Reuses one pooled pulse; clamps cyan/blue color, 0.10-0.18 alpha, and 0.20-0.35s unscaled duration. Re-scan, manual/combat close, and teardown release it; Radar persistence and marker colors are unchanged. |
| Unknown-objective localization | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs<br>Assets/02_Scripts/Config/Localization/Source/Localization.csv<br>Assets/02_Scripts/Config/Localization/LocalizationCatalog.asset | Player-facing unknown signal, Purple Core, interaction, and Curse error strings use stable Korean-first keys; the `?` Radar marker remains authoritative. |
| Purple Core reveal | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Visual starts at zero alpha/70% scale, reveals over 0.55s, keeps colliders disabled until ready, and idles only its visual root. |
| Purple Core Curse infiltration | Superseded | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | The later relay/Core interaction pass moves the Core sprite child while retaining the authoritative root position. |
| Tutorial QA Pass 2 tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Focused source/contract coverage compiles; Unity Test Runner execution is pending. |
| Tutorial QA Pass 2 Play Mode | Not Started | Tutorial at 480x270 | Verify delayed transmission, audio cue after assignment, two Radar scans, localized copy, Core reveal, transfer visibility, emergency return, and interruption cleanup. |

## Tutorial QA Presentation Pass 3 — 2026-09-03

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Map-independent supply completion | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Supply damage is enabled from Map onward; existing death authority converges Map, route, and approach paths on one collection step. |
| Radar focus/combat persistence | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Temp/PlayerRadarScanner.cs | Target focus preserves open state; ordinary combat no longer closes Radar. Manual and boss-owned paths remain. |
| Persistent signal relay | Implemented / Static Verified | Assets/01_Scenes/Tutorial.unity<br>Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Authored at (-8.9, 15.33), remains visible after one-shot interaction, and reuses its renderer for one unscaled purple pulse. |
| Stationary Curse infiltration | Superseded | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Replaced by Core-sprite travel plus anchored ship-origin corruption; the authoritative Core root remains stationary. |
| Destroyed-reference cleanup | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Owned tweens stop first and Unity-null checks prevent refresh of a destroyed `PlayerVisualStateController`. |
| Tutorial QA Pass 3 tests | Implemented / Static Verified | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Test source compiles; Unity Test Runner execution pending. |
| Tutorial QA Pass 3 Play Mode | Not Started | Tutorial at 480x270 | Verify no-map destruction, Radar persistence, relay position/pulse, stationary infiltration, and teardown safety. |

## Tutorial QA Cinematic Presentation Pass - 2026-09-03

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Safe-viewport target presentation | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | Discovery records supply/wreck targets; focus begins only after 0.15s inside X 0.10-0.90 / Y 0.12-0.88. Invalid/interrupted targets do not complete guidance. |
| Purple relay/Core effects | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/03_Prefabs/Purple.prefab<br>Assets/01_Scenes/Tutorial.unity | Existing Purple ring is serialized once, driven with unscaled DOTween, and reset before pool release. Core reveal retains collider-safe visual-only behavior. |
| Core interaction cinematic | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Existing letterbox and owner-scoped camera framing precede Core-sprite travel and ship-origin corruption; background/player state changes only under full ScreenFader coverage. |
| Serialized mystery fallback copy | Implemented / Static Verified | Assets/01_Scenes/Tutorial.unity | Raw player-facing `???` fallbacks are replaced; stable localized IDs and the `?` Radar marker remain unchanged. |
| Focused EditMode coverage | Implemented / Not Run | Assets/02_Scripts/Dialogue/Editor/Tests/Phase2CStoryDialogueTests.cs | Adds viewport dwell/flicker, Purple prefab, transfer/fade ordering, and cinematic cleanup coverage. |
| Tutorial cinematic Play Mode | Not Started | Tutorial at 480x270 | Verify framing, pulse color/scale, reveal, transfer overlap, black-covered state switch, interruption cleanup, and the unchanged 28-step completion flow. |

## Tutorial Relay Takeover and Purple Core Interaction — 2026-09-03

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Real/Fake Operator relay handoff | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs<br>Assets/02_Scripts/Dialogue/Editor/Phase2CStoryDialogueInstaller.cs | Analysis precedes real cutoff; stable `FakeOperator` uses a concealed Operator display name and Curse theme. Only natural fake completion advances. |
| Map/Radar unknown-signal ownership | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Relay analysis clears broad Map guidance; one Radar-only `?` persists to Core reveal and lifecycle cleanup. |
| Purple Core damage response | Implemented / Static Verified | Assets/02_Scripts/Player/IDamageable.cs<br>Assets/02_Scripts/Player/Bullet.cs<br>Assets/02_Scripts/Tutorial/TutorialInteractionTarget.cs | Typed player-only receiver, 0.12s/4-damage contribution cap, threshold 15, no enemy health/death/reward path. |
| Shared Core acquisition authority | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs<br>Assets/02_Scripts/Dialogue/Phase2CStoryDialogueContracts.cs | `F` and threshold share one latch/conversation; simultaneous and duplicate requests fail closed. |
| Core-sprite travel cinematic | Implemented / Static Verified | Assets/02_Scripts/Tutorial/TutorialFlowController.cs | Sprite child travels and restores on cancellation; pooled Purple effect is stationary support/ship-origin corruption; Curse remains after full black. |
| Deterministic content/tests | Implemented / Static Verified | Assets/02_Scripts/Config/Localization/<br>Assets/02_Scripts/Dialogue/Editor/Tests/ | Nine graph sources and focused policy/progress tests compile; production Dialogue Database installation and Unity Test Runner are pending. |
| Tutorial pass Play Mode | Not Started | Tutorial at 480x270 | Tune threshold for each starting weapon; verify takeover pacing/theme, Radar-only marker, Core travel overlap, cancellation, and exact-once completion. |

## Boot Main Menu UI Authoring Migration Pass 1 — 2026-09-03

| Item | Status | Main Files/Assets | Notes |
|---|---|---|---|
| Serialized Boot view/bindings | Implemented / Static Verified | Assets/02_Scripts/UI/BootMainMenuView.cs<br>Assets/02_Scripts/UI/MainMenuController.cs | Runtime visual construction removed; missing references fail safely. |
| Editor installer | Implemented / Static Verified | Assets/02_Scripts/UI/Editor/BootMainMenuUIInstaller.cs | Scene-authored hierarchy, Undo support, existing EventSystem/ScreenFader reuse, no automatic scene save. |
| Read-only validator | Implemented / Static Verified | Assets/02_Scripts/UI/Editor/BootMainMenuUIInstaller.cs | Distinguishes missing root/component/view binding, missing typed children, wrong asteroid count, duplicates, inactive references, and missing sprites. |
| Idempotent repair/manual tuning preservation | Implemented / Static Verified | Assets/02_Scripts/UI/Editor/BootMainMenuUIInstaller.cs | Repairs partial backgrounds slot-by-slot; valid transforms, colors, sprites, names, and references are preserved. |
| Authored options/background | Implemented / Static Verified | Assets/02_Scripts/UI/SharedOptionsMenuUI.cs<br>Assets/02_Scripts/UI/MainMenuController.cs | Thirty stars, seven asteroids, and the Curse passer are serialized; runtime only initializes behavior/motion. |
| EditMode authoring tests | Implemented / Not Run | Assets/02_Scripts/UI/Editor/Tests/BootMainMenuUIAuthoringTests.cs | Partial component, child, and null-view-binding fixtures added; Unity Test Runner execution pending. |
| Boot scene installation | Partial / Needs Retest | Assets/01_Scenes/Boot.unity | The user reproduced the partial install; rerun Install then Validate and save manually after verification. |
| Boot Play Mode | Not Started | Boot at 480x270 | Verify Continue/New Game/Settings/Exit, navigation, audio, fades, and no runtime-created duplicate UI. |
