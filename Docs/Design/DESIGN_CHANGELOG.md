# VOID SCRAPPER 설계 변경 원장

작성 기준일: 2026-08-30

## 문서 목적과 판정 기준

이 문서는 기존 프로토타입 문서인 Docs/Reference/VOID SCRAPPER_잔해해역의 수확자_프로토타입.pdf와 현재 Unity 프로젝트를 비교한 변경 원장이다. 새 시스템 설계서의 대체물이 아니며, 구현되어 있다는 이유만으로 수치나 콘텐츠를 최종 설계로 확정하지 않는다.

근거 우선순위는 현재 1차 제작 코드, 현재 직렬화 프리팹·씬·ScriptableObject, AGENTS.md, 기존 프로토타입 문서 순서다.

- Confirmed Design: AGENTS.md 또는 현재 구조와 문서에서 설계 의도가 명시적으로 확인됨
- Implementation: 현재 프로젝트에서 동작 경로와 연결 상태가 확인되지만 최종 설계 여부는 미확정
- Testing: 구현되어 있으나 수치·콘텐츠·사용성 검증이 더 필요함
- Planned: 정의나 진행 경로는 있으나 완전한 플레이 경로가 없음
- Deferred: 프로토타입 범위에서 명시적으로 제외되었고 현재도 완성 근거가 없음
- Legacy: 현재 설계의 권위 있는 경로가 아니거나 호환 목적으로 남아 있음

이번 감사는 정적 분석만 수행했다. Unity Editor 및 Play Mode 검증은 수행하지 않았다.

### 게임 개요와 핵심 루프

Prototype document:
- 2D 탑다운 픽셀 슈팅 로그라이트이며, 탐사 → 수확 → 전투 → 귀환 → 정착지 성장의 순환을 제시한다.
- 전투는 적 전멸 자체보다 수확 경로와 고가치 장소를 확보하는 수단이다.

Current project:
- RunManager, RunContext, ExpeditionBootstrap, SettlementController가 같은 큰 순환을 유지한다.
- 런은 함선·무기 트리·해역·재화·특성·Reinforcement·화물·보스 진행을 함께 보존한다.

Status:
- Confirmed Design

Change:
- 핵심 루프는 유지됐지만 런 상태와 성장 축이 프로토타입보다 크게 세분화됐다.

Reason:
- AGENTS.md에서 현재 핵심 루프와 전투의 역할을 동일하게 재확인한다.

System-design impact:
- 새 설계서는 핵심 루프를 유지하고, 각 단계에 화물 관리·작전 목표·해역 효과·지역 연속 진행을 하위 루프로 추가해야 한다.

### 프로토타입 범위

Prototype document:
- HUD, 레이더, 수확, 전투, 상점, 코어, 지역 1 보스, 결과 화면, 정착지 성장까지를 검증 범위로 잡고 추가 지역·보스와 장기 콘텐츠는 제한한다.

Current project:
- 지역 1~3과 최종 네트워크, 캠페인 보스 정의, 튜토리얼, 서사 특성, 다수 이벤트·NPC·Reinforcement·영구 성장 경로가 추가됐다.
- 지역 2 Salvage Devourer와 지역 3 Phase Gatekeeper는 전용 프리팹·컨트롤러·직렬화 실행 경로가 추가됐다.
- 정착지 Route Core 연결과 최종 보스 프리팹은 여전히 완성된 직렬화 경로가 없다.

Status:
- Implementation
- Planned

Change:
- 단일 지역 프로토타입에서 지역 1~3의 전용 보스 조우를 포함한 다지역 캠페인으로 확대됐다. 정착지 후반 게이트와 최종 조우는 아직 부분 상태다.

Reason:
- 추가 지역과 보스는 기존 문서에서 프로토타입 이후 범위로 명시됐다.

System-design impact:
- 새 설계서는 지역 1~3 조우를 Implementation·Testing으로 다루고, 정착지 후반 게이트와 최종 조우는 Planned 상태로 분리해야 한다.

### 플레이어 목표와 Expedition 흐름

Prototype document:
- 단기 목표는 한 번의 탐사 후 귀환, 중기 목표는 정착지 복구, 장기 목표는 심층 진입과 보스 격파다.
- 정착지 → 탐사 → 코어 → 보스 → 안전 귀환 또는 심층 진입의 흐름을 제시한다.

Current project:
- ExpeditionOperationController가 매 탐사마다 고가치 잔해, 신호 조사, 방어망 교란 중 하나의 작전 목표를 만든다.
- 지역 1 → 지역 2 → 지역 3은 한 런 안에서 이어지며 RunContext가 체력·방어도·런 재화를 계승한다.
- 최종 네트워크는 지역 3 이후 즉시 진입하지 않고 정착지 캠페인 조건을 거쳐 별도 출격한다.

Status:
- Implementation
- Testing

Change:
- 자유 탐사 중심 목표에 지역별 작전 목표가 추가됐고, 최종 지역은 일반 심층 이동과 분리됐다.

Reason:
- Reason not documented

System-design impact:
- 단기 목표를 탐사 작전, 선택적 수확, 코어 신호 확보, 보스, 귀환 결정으로 재정의하고 최종 출격은 별도 캠페인 단계로 표시해야 한다.

### HUD

Prototype document:
- HP·Armor·XP, Credits·Scrap·Core, 상호작용·경고, 우하단 레이더를 중심으로 제시한다.

Current project:
- ExpeditionHUD와 Expedition 씬은 HP·Armor, 아이콘 채움형 Dash, Core Signal, 화물 적재, Credits·Scrap·Core·Alloy·Tuning Chips, Reinforcement, 무기 열·충전, 상태 효과, 작전 브리핑과 입력 힌트를 연결한다.
- 현재 HUD에는 런 XP 레벨 게이지가 권위 있는 성장 UI로 연결되어 있지 않다.

Status:
- Implementation
- Testing

Change:
- XP 중심 HUD가 Core Signal·Cargo·Reinforcement·상태 정보 중심으로 전환됐고 표시 재화가 늘었다.

Reason:
- AGENTS.md가 현재 Expedition HUD의 정보 우선순위와 Dash·Reinforcement의 아이콘 채움 방식을 명시한다.

System-design impact:
- 새 HUD 명세는 현재 계층과 데이터 소유자를 기준으로 다시 작성하고 XP 게이지는 레거시 항목으로 분리해야 한다.

### 조작

Prototype document:
- WASD 이동, 좌클릭 발사, 우클릭 Dash, Q 누르기 레이더, E 상호작용을 제시한다.

Current project:
- PlayerControls.inputactions는 이동 WASD, 발사 좌클릭, Dash 우클릭, Radar Q, 빠른 스캔 Mouse 4, 상호작용 F, Inventory E, Map Tab, Reinforcement R, Dismantle·Field Drop G, Cancel·Menu Esc를 정의한다.
- UI는 InputBindingUtility와 InputAction 표시 문자열을 사용한다.

Status:
- Confirmed Design

Change:
- 상호작용이 E에서 F로 이동했고 Inventory, Map, Reinforcement, Dismantle, 빠른 스캔 조작이 추가됐다.

Reason:
- AGENTS.md가 현재 목표 바인딩과 표시 문자열 규칙을 명시한다.

System-design impact:
- 조작표를 현재 Input Actions 기준으로 교체하고 모든 UI 키 표시는 재바인딩 대응 규칙으로 명시해야 한다.

### Radar

Prototype document:
- Q를 누르는 동안 스캔하고, 잠시 유지되는 지역 정보만 표시하는 시스템이다.
- Shotgun은 도발, Sniper는 적 경보 억제와 넓은 시야, Machine Gun은 기본 레이더를 가진다고 설명한다.

Current project:
- PlayerRadarScanner는 Q 충전 스캔, 열린 상태에서 Mouse 4 빠른 스캔, 반경 절반의 저주기 수동 레이더, 이동에 따른 지도 공개, 스캔 기억 시간을 구현한다.
- Sniper의 은폐·감지 보조는 기본 속성보다 sn_stealth_scan 특성 단계에 연결된다.
- Shotgun 도발과 무기별 경보 반응은 RadarTarget별 허용 플래그에 의존하며, 다수 일반 적 프리팹은 이를 비활성화한다.

Status:
- Testing

Change:
- 단일 Q 스캔에서 수동·빠른·근거리 탐지와 지도 공개가 결합된 시스템으로 확장됐다.
- 프로토타입의 무기별 레이더 규칙은 현재 모든 적에게 공통 적용되지 않는다.

Reason:
- Reason not documented

System-design impact:
- 스캔 종류, 반경, 기억 시간, 지도 공개 관계를 분리해 명세하고 무기별 반응은 확정 전 테스트 항목으로 남겨야 한다.

### Map과 경로 계획

Prototype document:
- 레이더는 전체 지도를 대체하지 않으며 최근 스캔한 지역 정보만 보여 준다.

Current project:
- MapDiscoveryController가 탐사 공개 마스크와 발견한 목표를 유지한다.
- ExpeditionRoutePlanner는 최대 5개 경유지, 자동 다음 경유지, 월드 가이드를 지원한다.
- Tab 지도와 E Inventory는 ExpeditionMenuController가 관리한다.

Status:
- Implementation
- Testing

Change:
- 별도 전체 지도, 탐사 안개, 경로 계획 시스템이 추가돼 레이더와 역할이 분리됐다.

Reason:
- Reason not documented

System-design impact:
- Radar는 탐지 수단, Map은 누적 공간 정보, Route Planner는 플레이어 계획 도구로 별도 장을 구성해야 한다.

### 함선과 무기 트리

Prototype document:
- 하나의 공통 함선 프레임에서 Shotgun·Sniper·Machine Gun 트리를 출격 전 선택한다.
- 무기별 HP와 고유 레이더·전투 특성을 제시한다.

Current project:
- 01_basic_ship, 02_shotgun_ship, 03_sniper_ship이 각각 기본 무기 트리와 해금 비용을 가진 별도 ShipDefinition이다.
- 세 함선의 직렬화 HP는 모두 20이며 Machine Gun 함선만 수확물 피해 보너스가 확인된다.
- SettlementController의 기본 ID basic_ship과 Machine Gun 자산 ID muchingun_ship이 일치하지 않으며, 튜토리얼 후 경로는 해금된 함선을 다시 선택해 보정한다.
- ShipTraitTreePanel에는 공용·Machine Gun·Shotgun·Sniper 영구 트리가 있으나 일부 비용과 조건은 시험 값 성격이 강하다.

Status:
- Testing

Change:
- 공통 프레임의 독립 무기 선택에서 함선 해금과 무기 정체성이 결합된 구조로 이동했다.
- 프로토타입의 무기별 HP와 Shotgun 5발은 현재 데이터와 일치하지 않는다. 현재 Shotgun은 6발이다.

Reason:
- Reason not documented

System-design impact:
- 함선과 무기 선택의 결합 여부, 함선별 패시브, 최종 수치, ID 불일치를 확정하기 전까지 테스트 상태로 기록해야 한다.

### Run 성장과 Trait

Prototype document:
- 적·수확·이벤트에서 XP를 얻어 현재 런 레벨을 올리고, 레벨업마다 공용 2개와 무기 전용 1개 중 하나를 고른다.

Current project:
- CurrencyType.Experience와 RunContext.CurrentLevel은 레거시 호환 필드로 남아 있으나 현재 보상 자산에 XP 지급이 없고 런 레벨업 구동 경로도 확인되지 않는다.
- 현재 런 성장은 Trait 획득·교체·강화, 특별 컨테이너, 보스 선택 보상, 상점·암시장, Tuning Chips를 통한 보유 Trait 강화로 구성된다.
- Trait은 희귀도, 선행 조건, 최대 레벨, 무기 분기, 필드 폐기·분해 가능 여부를 데이터로 가진다.

Status:
- Legacy
- Implementation

Change:
- XP 자동 레벨업 선택 구조가 현재 권위 있는 성장 경로에서 빠지고, 획득원 기반 Trait 성장으로 대체됐다.

Reason:
- CoreTypes.cs가 Experience를 레거시로 명시한다. 대체 성장 구조의 설계 이유는 문서화되지 않았다.

System-design impact:
- XP 레벨업 표와 XP HUD를 삭제 또는 레거시 부록으로 이동하고, Trait 획득원·중복 강화·교체·분해·Tuning 규칙을 새로 명세해야 한다.

### Pixel Curse

Prototype document:
- 현재 프로토타입 문서에는 영구 서사 패시브로서의 Pixel Curse 규칙이 없다.

Current project:
- 45_pixel_curse.asset은 숨김·영구 서사 Trait·부정 효과·필드 폐기 및 분해 금지로 정의된다.
- 튜토리얼은 감염, 시각 변환, 구조 신호 대화와 정착지 첫 대화까지 연결한다.

Status:
- Confirmed Design

Change:
- 일반 Trait과 구분되는 지속형 서사 디버프와 전용 시각 규칙이 추가됐다.

Reason:
- AGENTS.md가 Pixel Curse의 지속성, 획득 풀 제외, Debuff UI, 시각 규칙을 명시한다.

System-design impact:
- Trait 구조를 재사용하되 획득·저장·표시·비분해·비드롭 예외와 서사 진행을 별도 명세해야 한다.

### Reinforcement

Prototype document:
- 수확물의 낮은 확률 함선 증원과 상점 구매 항목을 언급하지만, 장비 슬롯·충전·재사용 구조는 정의하지 않는다.

Current project:
- Reinforcement Catalog에는 공용 및 무기별 41개 정의가 연결돼 있다.
- PlayerReinforcementController가 장착, R 사용, 충전 수, 순차 재충전, 런 간 지역 이동 보존, 생성물 수명과 소유권을 관리한다.
- 획득물은 F 교체·획득과 G 분해를 지원하며 Emergency Return은 무작위 드롭에서 제외된다.

Status:
- Implementation
- Testing

Change:
- 단순 일회성 보조품 개념이 능동 장비 슬롯과 충전식 지원 체계로 확장됐다.

Reason:
- AGENTS.md가 PlayerReinforcementController의 권위 범위를 명시한다.

System-design impact:
- 장비 유형, 획득원, 교체, 충전·재충전, 무기 제한, 생성물 소유권, HUD 계약을 독립 시스템으로 추가해야 한다.

### Cargo

Prototype document:
- Credits·Scrap·Core를 즉시 보유하며 별도 적재량, 중량, 자동 수집 규칙은 설명하지 않는다.

Current project:
- PlayerCargoController가 Scrap·Core·Stabilized Alloy에 적재 중량을 적용한다. 기본 용량은 100, 기본 중량은 각각 2·12·2다.
- 80%와 95% 적재 구간에서 이동·Dash 불이익이 생기며, 과적 시 수동 회수, 재화별 자동 회수 전환, 투기가 가능하다.
- Credits는 런 재화지만 화물 중량에는 포함되지 않는다.

Status:
- Testing

Change:
- 수집 재화에 화물 용량, 중량, 과적 단계와 Inventory 관리가 추가됐다.

Reason:
- Reason not documented

System-design impact:
- 재화 소유와 화물 적재를 분리하고, 중량·임계값·이동 불이익·회수·투기·긴급 귀환 보존량을 명세해야 한다.

### Currency

Prototype document:
- Heal은 즉시 효과, XP는 런 레벨, Credits는 런 전용, Scrap과 Core는 영구 성장 재화로 구분한다.

Current project:
- Credits는 상점·NPC 서비스용 런 재화이며 귀환 시 영구 저장되지 않는다.
- Scrap·Core·Stabilized Alloy는 귀환 결과에 따라 PermanentProgress에 저장된다.
- Tuning Chips는 런 안에서 보유 Trait 강화에 사용되고 귀환 시 저장되지 않는다.
- Experience는 레거시이며 현재 보상 자산의 지급 경로가 없다.

Status:
- Implementation
- Testing

Change:
- Alloy와 Tuning Chips가 추가됐고 XP는 현재 성장 경제에서 제외됐다.
- Scrap·Core는 화물 용량과 귀환 방식에 따라 실제 보존량이 달라진다.

Reason:
- Experience의 레거시화만 코드에 명시돼 있으며 나머지 경제 개편 이유는 문서화되지 않았다.

System-design impact:
- 각 재화의 획득원, 런 내 사용처, 화물 중량, 귀환 시 처리, 영구 사용처를 하나의 경제 표로 다시 작성해야 한다.

### Map 생성과 경계

Prototype document:
- 80×80 사각 맵과 네 방향 월드 래핑, 시작 안전 반경 10, 중요 지점 최소 거리 20을 제시한다.

Current project:
- New Map Generation Config.asset은 지역 1 120×120, 지역 2 128×128, 지역 3 116×116, 최종 96×96을 정의한다.
- 시작 안전 반경은 18, 중요 지점 최소 거리는 28이다.
- WorldWrapController는 Obsolete이며 현재는 경고·감속·안쪽 밀기·하드 클램프를 가진 유한 경계를 사용한다.
- POI 군집, 환경 장식, 이동 소형 운석, 지역별 적·보상 배율과 세 가지 해역 환경 변형이 추가됐다.
- 지역 2는 맵 상단 Core와 그 아래 전투 회랑을 예약한다. 현재 회랑 설정은 반폭 9, 하향 길이 48이다.
- 지역 3은 Core·현장 기지·상점을 생성하지 않고 Phase Gatekeeper와 길이 4의 반사판 8개를 전용 전투 기반으로 생성한다.

Status:
- Implementation
- Testing

Change:
- 고정 80×80 래핑 맵이 지역별 크기의 유한 맵, 군집형 배치, 지역 2 회랑과 지역 3 전용 보스 기반으로 교체됐다.

Reason:
- Reason not documented

System-design impact:
- 맵 크기와 배치 수치는 테스트 값으로 표시하고, 유한 경계·POI 군집·지역 스케일·해역 변형과 지역별 보스 공간 예외를 생성 규칙으로 기록해야 한다.

### 수확 오브젝트

Prototype document:
- 보급 컨테이너, 고가치 잔해, 파괴된 선체가 Credits·Scrap·회복·낮은 확률 증원을 제공한다.
- 운석은 보상 없이 탄환과 이동을 막는 지형이다.

Current project:
- 세 수확 유형은 유지되며 RewardDefinition과 RewardDropper로 Credits·Scrap·Alloy·회복·Trait·Reinforcement를 조합한다.
- 고가치 잔해는 작전 목표와 연결될 수 있고, 파괴된 선체의 증원 진입은 경고 이동을 포함한다.
- 소형 이동 운석과 대형 운석, 보스 구역 엄폐 배치가 생성 설정에 포함된다.

Status:
- Implementation
- Testing

Change:
- 수확 보상이 새 재화와 장비 체계로 확장됐고, 고가치 잔해가 작전·적 역할과 연결됐다.

Reason:
- Reason not documented

System-design impact:
- 수확물별 내구도와 보상은 최종 밸런스와 분리해 표기하고, 작전·화물·경쟁 적과의 연결을 추가해야 한다.

### Enemy 전투 구조

Prototype document:
- Patrol → Alert → Combat → Search → Return과 Taunt 상태, 시야·피격·총성·동료 사망·코어 활성화에 따른 전투 전파를 제시한다.
- Basic, Shotgun, Charging, 단일 Elite의 수치와 공격을 정의한다.

Current project:
- 기존 상태 구조는 유지되며 EnemyDefinition과 공격 패턴 데이터로 분리됐다.
- Elite는 Machine Gun·Shotgun·Charging으로 나뉘고 Melee Charger와 여러 보안·역할 적이 추가됐다.
- 시각 탐지는 누적 감지, 레이더 탐지, 총성 소음을 사용한다. Sniper의 무조건 비경보 규칙은 현재 기본 규칙이 아니다.
- Basic HP 8, Shotgun HP 14, Charging HP 12 등 일부 현재 수치는 프로토타입과 다르다.

Status:
- Implementation
- Testing

Change:
- 단일 전투 FSM은 유지하면서 적 종류, 공격 데이터, 감지 방식과 난이도 스케일이 확장됐다.

Reason:
- Reason not documented

System-design impact:
- 상태 전이와 전투 전파는 구조 명세로 유지하고, 개별 수치는 현재 테스트 데이터로 분리해야 한다.

### Enemy 역할

Prototype document:
- 일반 교전 적과 보스 중심이며, 수확 경쟁이나 목표 방어를 별도 역할 계층으로 정의하지 않는다.

Current project:
- EnemyRoleController는 Patrol, Defender, RivalHarvester, Scavenger 역할을 제공한다.
- Defender는 중요 지점을 방어하고, RivalHarvester는 수확 후 기지로 운반·이탈하며, Scavenger는 드롭을 훔쳐 복귀한다.
- Dormant·Preview·Active 시뮬레이션 단계로 원거리 행동과 비가역 행동을 제한한다.
- 임시 유인과 감속은 출처별로 관리돼 오래된 효과가 새 효과를 지우지 않는다.

Status:
- Confirmed Design

Change:
- 적이 단순 전투 장애물에서 목표 방어·자원 경쟁·회수 방해 행위자로 확장됐다.

Reason:
- AGENTS.md가 역할 상태 보존, 최신 유효 유인 우선, 최강 감속 우선 규칙을 명시한다.

System-design impact:
- 전투 상태와 전략 역할을 분리하고, 각 역할의 목표·이탈·화물·시뮬레이션 조건을 명세해야 한다.

### Events와 현장 작전

Prototype document:
- Rescue Signal과 50% 안전 보상·50% 증원 전투의 Unknown Device 두 이벤트를 제시한다.

Current project:
- Rescue Signal과 Unknown Device 외에 제한 시간 안정화형 Unstable Reactor와 다중 방어 웨이브형 Black Box가 있다.
- 성공 이벤트, 특수 컨테이너, NPC 구조, Elite 화물, Rival Harvester가 Objective Signal을 제공할 수 있다.
- 별도의 ExpeditionOperationController가 지역당 하나의 탐사 작전을 선택한다.

Status:
- Implementation
- Testing

Change:
- 이벤트가 네 유형으로 늘고, Core 발견과 보스 보상에 연결되는 신호 경제 및 지역 작전이 추가됐다.

Reason:
- Reason not documented

System-design impact:
- 이벤트별 시작·성공·실패·웨이브·보상과 Objective Signal 기여도를 명시하고 지역 작전과 분리해 설명해야 한다.

### Shops, 현장 기지와 NPC

Prototype document:
- 중립 상점은 수리·Trait·함선 증원을 판매한다.
- 보호막 파괴 시 모든 상점이 해당 런 동안 적대하며, 격파 보상과 사용 Credits 일부 환급을 제공한다.

Current project:
- 중립·경고·전역 적대 구조는 유지된다.
- 상점은 수리, 무작위 Trait 3개, Reinforcement 1~3개를 판매하며 지역별 가격 배율을 적용한다.
- 런 범위 Maintenance Bay가 현재 장비 외 Reinforcement를 보관한다.
- 중립 안전 구역, 전력 그룹·보안 노드·지역별 포탑과 드론 수가 추가됐다.
- 현장 기지와 네 서비스 유형 NPC가 Credits·자원·Trait 선택과 연결된다.

Status:
- Implementation
- Testing

Change:
- 함선 증원은 정식 Reinforcement 재고로 대체됐고, 상점 방어와 현장 서비스 생태계가 확장됐다.

Reason:
- Reason not documented

System-design impact:
- 거래 재고, 가격, 적대 전파, 방어망, 파괴 보상, Maintenance Bay, 현장 기지·NPC 서비스를 분리해 명세해야 한다.

### Core 발견과 활성화

Prototype document:
- Core는 선택적으로 발견해 2초 상호작용하면 즉시 보스를 시작하고 구역을 봉쇄한다.

Current project:
- 2초 상호작용은 유지된다.
- 지역 1·2에서 사용하는 Core.prefab은 Objective Signal 2개 전까지 숨김·상호작용 불가이며 직접 월드 발견도 비활성화한다.
- Objective Signal 3개는 보스 희귀 보상을 보장하고 4개는 보상 선택지를 하나 늘린다.
- 일반 Core 경로는 활성화 시 Core 보상을 드롭하지만, 지역 2 전용 경로는 Core 2개 지급을 보스 사망 시점으로 미루고 중복 지급을 차단한다.
- 지역 3은 Core를 생성하지 않으며, 맵에 미리 생성된 Phase Gatekeeper의 근접 트리거가 전투 도입을 시작한다.

Status:
- Implementation
- Testing

Change:
- 지역 1·2 Core는 탐사 신호로 위치를 해금하는 목표 허브로 바뀌었고, 지역 3은 Core 없이 전용 보스 조우로 분기한다.

Reason:
- Reason not documented

System-design impact:
- 지역 1·2의 Core 발견·공개·활성화와 지역 2의 보상 후지급 예외를 명세하고, 지역 3의 Core 없는 전투 진입은 별도 흐름으로 작성해야 한다.

### Region 1 Boss

Prototype document:
- Sector Administrator 한 보스를 HP 140, 구역 레이저·산탄·추적 충전탄, HP 30%의 Phase 2와 회전 레이저로 정의한다.

Current project:
- Boss.prefab과 BossPatternController가 Sector Administrator 전투를 구현한다.
- 직렬화 Phase 2 임계값은 현재 HP 50%이며 보호막·도입 연출·구역 레이저·산탄·충전 공격과 특수 회전 패턴을 가진다.
- 첫 격파는 Sector Stabilizer와 sector barrier Trait을 제공하고 지역 2를 해금한다.
- 재도전에서는 별도 Raider Commander 프리팹을 사용할 수 있다.

Status:
- Implementation
- Testing

Change:
- 지역 1 캠페인 보상과 재도전 보스가 추가됐고 Phase 전환 수치가 프로토타입과 달라졌다.

Reason:
- Reason not documented

System-design impact:
- 패턴 구조와 캠페인 보상을 유지하되 HP·Phase 임계값은 최종 확정 전 테스트 수치로 표시해야 한다.

### Region 2: Salvage Devourer

Prototype document:
- 추가 지역과 보스는 프로토타입 범위 밖이다.

Current project:
- BossCampaign_Region2.asset은 Salvage Devourer, Matter Compressor, matter reconstructor Trait, Core 2개를 정의한다.
- Core.prefab의 region2BossPrefab은 PF_Boss_SalvageDevourer_FrigateTriad.prefab에 직렬화 연결돼 있다.
- Expedition 씬은 PF_Region2BossCorridorRuntime.prefab을 연결한다. 맵 생성기는 상단 Core와 전투 회랑을 예약하고, 전용 도입 연출이 함대 진입·편대 추종·세로 스크롤을 인계한다.
- 세 호위함은 개별 파츠 체력과 하나의 집계 EnemyHealth를 사용한다. 생존 수 3·2·1에 따라 일제사격, 조준사격, 경고 구역, 제한 유도탄, 벽 도탄, 레이저, 회전탄 패턴이 바뀐다.
- 마지막 치명타는 경고된 세로 돌진을 완료한 뒤 사망 처리로 넘어가며, Core 2개는 보스 사망 시 중복 방지 경로로 지급된다.

Status:
- Implementation
- Testing

Change:
- 캠페인 정의만 있던 상태에서 전용 3기 편대, 스크롤 회랑, 생존 수별 패턴과 사망·보상 흐름이 구현됐다.

Reason:
- Reason not documented

System-design impact:
- 현재 편대·회랑·패턴·최종 돌진·보상 구조를 Implementation으로 기록하되 수치와 플레이 감각은 Testing으로 남겨야 한다.

### Region 3: Phase Gatekeeper

Prototype document:
- 추가 지역과 보스는 프로토타입 범위 밖이다.

Current project:
- BossCampaign_Region3.asset은 Phase Gatekeeper, Phase Navigation Lens, phase afterimage Trait, Core 2개를 정의한다.
- Expedition 씬은 PF_Boss_PhaseGatekeeper.prefab과 PF_Region3_PhaseReflectorPlate.prefab을 ExpeditionMapGenerator에 직렬화 연결한다.
- 지역 3 생성 경로는 Core·현장 기지·상점을 0개로 만들고 전용 보스와 반사판 8개를 배치한다. 플레이어가 근접 트리거에 들어오면 도입 연출과 전투가 시작된다.
- 보스는 은폐 상태에서 최대 3회 반사되는 레이저를 한 주기당 3회 발사한 뒤 현재 5초 동안 피해 가능 상태가 되며, 공격 사이에 은폐 이동한다.
- BossDummyController에는 지역 3 캠페인 정의와 Return Beacon·Wormhole이 연결돼 있다.
- 다만 Core가 없는 지역 3에서 BossCampaign_Region3.asset의 Core 2개를 지급하는 전용 호출 경로는 정적 검색으로 확인되지 않았다.

Status:
- Implementation
- Testing

Change:
- 캠페인 정의만 있던 상태에서 Core 없는 전용 반사 레이저 조우와 출구 흐름이 구현됐다. 구성된 Core 보상의 지급 경로는 아직 불명확하다.

Reason:
- Reason not documented

System-design impact:
- 은폐·반사 레이저·노출 피해 창·반사판 배치를 Implementation으로 기록하되 수치는 Testing으로 두고, Core 2개 지급 규칙은 연결 확인 전 확정하면 안 된다.

### Boss 보상

Prototype document:
- 보스 사망 시 귀환 장치와 Credits·Scrap·Core·XP를 즉시 제공한다.

Current project:
- 캠페인 정의는 Core 수량과 보장 Boss Trait을 설정하고, BossRewardExitCoordinator가 기본 3개 선택 보상을 먼저 제시한다.
- 지역 1 Core 보상은 Core 활성화 시 지급되며, 지역 2 Core 보상은 보스 사망 시 보장 지급과 중복 방지를 거친다.
- Core를 생성하지 않는 지역 3은 보장 Trait과 출구 흐름은 연결됐지만, 설정된 Core 2개 지급 호출은 확인되지 않았다.
- Objective Signal 3개는 희귀 보상을 보장하고 4개는 네 번째 선택지를 추가한다.
- 보상 선택이 끝난 뒤에만 Return Beacon과, 가능할 경우 Wormhole이 생성된다.
- XP 보상은 현재 권위 있는 경로에서 확인되지 않는다.

Status:
- Implementation
- Testing

Change:
- 즉시 일괄 보상이 선택형 보상과 탐사 성과 연동 구조로 바뀌었고, Core 지급 시점은 지역별로 분기한다.

Reason:
- Reason not documented

System-design impact:
- 캠페인 고정 보상, 지역별 Core 지급 시점, 선택형 런 보상, 신호 보너스, 출구 생성 순서를 구분하고 지역 3 Core 지급 공백을 미확정으로 표시해야 한다.

### Return과 심층 진행

Prototype document:
- 비전투 중 긴급 귀환은 영구 재화 20%를 잃고, 보스 후 안전 귀환은 손실이 없다.
- 보스 후 Wormhole을 선택하면 즉시 손실 없이 심층으로 이동한다.

Current project:
- 긴급 귀환은 전투·보스전·정지 조건을 검사하고 준비 동작을 거친다.
- 기본 보존 한도는 최대 화물 용량의 70%이며 Scrap·Core·Alloy를 비례 축소한 뒤 우선순위로 빈 용량을 채운다. 고정 20% 손실이 아니다.
- 사망은 Scrap과 Alloy 50%를 보존하고 Core는 모두 잃는다.
- 안전 귀환과 최종 승리는 영구 화물 전량을 확정한다. Credits와 Tuning Chips는 영구 저장되지 않는다.
- 지역 1 → 2 → 3은 Wormhole로 진행하지만 지역 3 뒤에는 즉시 최종 지역으로 이어지지 않는다.

Status:
- Implementation
- Testing

Change:
- 귀환 손실 규칙이 화물 용량 기반으로 바뀌고 사망·안전 귀환·최종 승리의 정산 규칙이 분리됐다.

Reason:
- Reason not documented

System-design impact:
- 귀환 유형별 허용 조건, 준비 취소, 자원별 보존, 지역 이동 시 계승 상태를 정확한 정산 표로 작성해야 한다.

### Settlement 성장

Prototype document:
- Hangar, Engine, Recovery Center, Weapon Lab을 각각 3단계까지 Scrap과 Core로 업그레이드한다.

Current project:
- PermanentProgress와 SettlementController는 기존 다단계 건물 업그레이드 필드를 Legacy로 취급한다.
- 현재 건물 진행은 캠페인 부품과 선행 복구에 의해 순차적으로 열리는 이진 복구 완료 상태다.
- 영구 성장의 실제 지출 축은 함선 해금, Ship Trait Tree의 Scrap·Core, 5종 Sector Technology의 Alloy다.
- Sector Technology는 각 3레벨과 2·3·5 Alloy 비용을 사용하지만 코드 명칭이 Prototype 수치임을 드러낸다.
- 명시적 BuildingDefinition 자산은 Hangar 하나만 확인되고 나머지는 fallback 정의에 의존한다.

Status:
- Implementation
- Testing

Change:
- 4개 건물의 재화 기반 3단계 성장에서 캠페인 복구 게이트와 별도 영구 트리·기술 성장으로 전환됐다.

Reason:
- 코드가 기존 다단계 건물 경로를 Legacy로 명시한다. 새 성장 구조의 설계 이유는 문서화되지 않았다.

System-design impact:
- 건물 복구, 함선 해금, 영구 Trait Tree, Sector Technology를 서로 다른 성장 축으로 재작성하고 시험 비용을 확정 값으로 취급하지 않아야 한다.

### Campaign 진행

Prototype document:
- 지역 1 보스 이후 더 깊은 해역으로 가는 장기 목표만 제시하며 완전한 캠페인 게이트는 범위 밖이다.

Current project:
- 지역 1 격파는 지역 2, 지역 2 격파는 지역 3을 해금한다.
- 지역 1~3 보스는 각각 Route Core 부품을 제공하고, 세 부품 수집 → 조립 → 활성화 → 정착지 방어 → 최종 출격 상태를 PermanentProgress가 보존한다.
- SettlementRouteCoreController가 이 흐름을 구현하지만 해당 스크립트 GUID는 현재 씬과 프리팹에 직렬화되어 있지 않다.

Status:
- Planned

Change:
- 다지역 보스 부품 기반 캠페인 구조가 추가됐지만 정착지에서의 완전한 실행 경로는 연결되지 않았다.

Reason:
- Reason not documented

System-design impact:
- 저장 상태 모델과 실제 플레이 가능 경로를 구분하고, Route Core 상호작용·정착지 방어 연결은 미구현 의존성으로 표시해야 한다.

### Tutorial

Prototype document:
- 별도 제작형 튜토리얼 흐름을 상세히 정의하지 않는다.

Current project:
- TutorialFlowController는 이동·조준·발사·Dash·Radar·Map·경로 핑·수확·화물·Reinforcement·전투·Unknown Signal·Pixel Curse·Emergency Return까지 28단계를 관리한다.
- 40×40 유한 튜토리얼 맵에서 운영 시스템을 재사용하고, 실제 긴급 귀환 후 첫 정착지 대화를 예약한다.

Status:
- Implementation
- Testing

Change:
- 핵심 시스템과 서사 도입을 묶은 전용 온보딩 흐름이 추가됐다.

Reason:
- Reason not documented

System-design impact:
- 튜토리얼 단계와 각 운영 시스템의 교육 목표, 완료 조건, 실패·누락 fallback을 별도 명세해야 한다.

### Story

Prototype document:
- 붕괴한 물류망과 Scrapper 조종사라는 세계관 및 장기 목표를 제시하지만 구현 대화 흐름은 제한적이다.

Current project:
- Dialogue System 데이터베이스에는 튜토리얼, 구조 후 대화, 첫 정착지, 네 NPC 서비스, 일반 보스 전·후 통신 등 10개 Conversation이 있다.
- 튜토리얼과 첫 정착지·NPC 서비스는 사용 경로가 확인된다.
- 일반 보스 전·후 통신은 현재 씬·프리팹 사용처가 확인되지 않으며 지역별 보스 서사, Route Core 연출, 최종 보스·엔딩 대화는 없다.

Status:
- Planned
- Implementation

Change:
- 세계관 개요에서 실제 대화 기반 튜토리얼·서비스 서사로 확장됐지만 캠페인 후반 서사는 부분 구현 상태다.

Reason:
- AGENTS.md가 Dialogue System을 권위 있는 서사 대화 체계로 지정한다.

System-design impact:
- 구현된 Conversation과 계획된 캠페인 비트를 구분하고, 지역별 보스·부품·최종 출격·엔딩의 누락을 명시해야 한다.

### Final Boss와 Ending

Prototype document:
- 최종 보스와 엔딩은 프로토타입 범위 밖이다.

Current project:
- BossCampaign_Final.asset은 Null Dispatcher와 FinalVictory 상태를 정의한다.
- CoreObject와 BossDummyController에는 최종 보스전·정착지 지원 단계·최종 승리 처리 코드가 있다.
- Core.prefab에는 finalBossPrefab이 연결되지 않아 정상 경로로 최종 보스전을 시작할 수 없다.
- AGENTS.md에는 거대 구형 외피, 패턴 파손, 보라색 Pixel Curse 내부 노출의 3단계 시각 방향이 있으나 실제 전용 보스 자산과 엔딩 시퀀스는 확인되지 않는다.

Status:
- Planned
- Deferred

Change:
- 최종 진행 상태와 시각 방향은 생겼지만 전용 조우와 엔딩 콘텐츠는 아직 플레이 가능한 완성 경로가 아니다.

Reason:
- 프로토타입 문서가 최종 보스·장기 콘텐츠를 범위 밖으로 두었다. 현재 미연결 상태의 추가 이유는 문서화되지 않았다.

System-design impact:
- 최종 보스는 확정된 시각 원칙, 존재하는 진행 훅, 미정 전투·보상·엔딩을 구분해 계획 장으로만 작성해야 한다.

## 감사 중 확인된 근거 공백

- Core를 생성하지 않는 지역 3에서 BossCampaign_Region3.asset의 Core 2개를 지급하는 호출 경로
- SettlementRouteCoreController가 연결된 정착지 오브젝트 또는 프리팹
- 실제 정착지 방어 콘텐츠와 그 완료 이벤트 연결
- Core.prefab의 finalBossPrefab 연결 및 Null Dispatcher 전용 전투 자산
- 지역별 보스 서사, 최종 출격, 엔딩 Conversation 또는 연출 자산
- 현재 테스트 수치를 최종 설계로 승인한 별도 근거 문서

이 항목들은 저장 상태나 코드 훅이 있다는 이유만으로 구현 완료 또는 확정 설계로 승격하면 안 된다.
