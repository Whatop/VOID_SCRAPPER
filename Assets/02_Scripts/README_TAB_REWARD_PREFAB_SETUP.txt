VOID SCRAPPER - Tab 상태창 / 재화 픽업 수정

[수정 파일]
- UI/PlayerBuildStatusPanelUI.cs
- RunRuntime/RunRuntimeTraitStore.cs
- RunRuntime/RunTraitEffectApplier.cs
- Player/PlayerRuntimeBonusState.cs

[변경 내용]
1. Tab 기체 슬롯 이미지가 ShipDefinition.PreviewSprite를 사용합니다.
2. Tab 기체 정보에 현재 런의 튜닝 칩 보유량을 표시합니다.
3. Tab 상태창에서 액티브/런타임 패시브를 G로 필드 드랍할 수 있습니다.
4. 패시브를 드랍하면 해당 레벨의 런타임 효과도 역적용됩니다.
5. 영구 적용 패시브는 필드 드랍할 수 없습니다.

[PlayerBuildStatusPanelUI 신규 연결]
Fixed Ship Slot
- Tuning Chip Value Text: 튜닝 칩 수량 TMP 텍스트
- Tuning Chip Value Format: 기본값 {0}개

Field Drop
- Field Drop Key: G
- Active Slot Select Button: 현재 액티브 슬롯 전체에 붙인 Button
- Active Field Drop Button: 별도 액티브 드랍 버튼이 있을 때만 연결
- Passive Field Drop Button: 별도 패시브 드랍 버튼이 있을 때만 연결
- Field Drop Hint Text: "[G] ... 필드 드랍" 안내 텍스트. 선택 사항
- Active Drop Selected Indicator: 액티브가 드랍 대상으로 선택됐을 때 켤 테두리. 선택 사항
- Trait Drop Pickup Prefab: TraitPickup 컴포넌트가 붙은 패시브 필드 픽업 프리팹
- Field Drop Origin: Player 자식 DropOrigin 권장
- Field Drop Distance: 1.25
- Field Drop Block Seconds: 0.5

사용법
- Tab 상태창을 연다.
- 액티브 슬롯을 클릭하면 액티브가 드랍 대상으로 선택된다.
- 패시브 슬롯을 클릭하면 해당 패시브가 드랍 대상으로 선택된다.
- G를 누르면 선택 대상이 플레이어 뒤쪽에 드랍된다.
- 패시브 Lv.3을 드랍하면 Lv.2가 되고, 바닥에는 해당 패시브 픽업 1개가 생성된다.

[ShipDefinition]
- Preview Sprite에 정착지/Tab UI용 전용 프리뷰 이미지를 연결합니다.
- Machine Gun / Shotgun / Sniper Sprite는 인게임 월드 기체용입니다.

[PF_RewardCurrency 권장 구조]
PF_RewardCurrency
- Rigidbody2D
  - Body Type: Kinematic
  - Gravity Scale: 0
- CircleCollider2D
  - Is Trigger: ON
- RewardPickup
- VisualRoot
  - Bubble: SpriteRenderer
  - Icon: SpriteRenderer

RewardPickup 연결
- Sprite Renderer: Icon
- Credits Sprite: 크레딧 이미지
- Scrap Sprite: 스크랩 이미지
- Core Shard Sprite: 코어 조각 이미지
- Tuning Chip Sprite: 튜닝 칩 이미지
- Visual Root: VisualRoot
- Bubble Renderer: Bubble

[PF_RewardHeal 권장 구조]
PF_RewardHeal
- Rigidbody2D
- CircleCollider2D(Is Trigger ON)
- RewardPickup
- VisualRoot
  - Bubble: SpriteRenderer
  - Icon: SpriteRenderer

RewardPickup 연결
- Sprite Renderer: Icon
- Heal Sprite: 회복 이미지
- Visual Root: VisualRoot
- Bubble Renderer: Bubble

[RewardDropper 등록 위치]
아래 프리팹/오브젝트의 RewardDropper마다 연결합니다.
- Reward Pickup Prefab: PF_RewardCurrency
- Heal Pickup Prefab: PF_RewardHeal

필수 대상
- 기본/샷건/차징/엘리트 적 프리팹
- 보스 프리팹
- 보급 컨테이너
- 고가치 잔해
- 파괴된 선체
- 구조 신호 / 미확인 장치 / 불안정 원자로 / 블랙박스 이벤트 프리팹
- 적대 상점 보상을 사용하는 ShopStructure
- 적 화물 재드랍을 사용하는 EnemyCargoHold가 붙은 역할형 적

RewardDefinition은 각 오브젝트별 기존 보상 에셋을 그대로 연결합니다.
PF_RewardCurrency는 CurrencyType에 따라 아이콘을 자동 변경합니다.
PF_RewardHeal은 RewardDropper.DropHeal에서 InitializeHeal이 호출되어 회복 픽업으로 동작합니다.

[PoolManager 권장 등록]
자동 풀 생성이 있어 필수는 아니지만, 초기 생성 끊김 방지를 위해 등록을 권장합니다.
- PF_RewardCurrency: Initial 24 / Max 128
- PF_RewardHeal: Initial 8 / Max 32
- TraitPickup: Initial 4 / Max 32
- ReinforcementPickup: Initial 4 / Max 32

[주의]
- NPC 보상 캡슐의 튜닝 칩은 RewardCapsule에서 현재 직접 지급합니다. 물리 픽업 프리팹 연결 대상이 아닙니다.
- Trait Drop Pickup Prefab이 비어 있으면 Tab 패시브 필드 드랍은 거부됩니다.
- PlayerReinforcementController의 Drop Pickup Prefab은 액티브 필드 드랍 비주얼을 위해 연결하는 것을 권장합니다.
