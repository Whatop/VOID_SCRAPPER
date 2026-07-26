VOID SCRAPPER - 코어 방전 유지 / 월드 자원 회수 업데이트

==================================================
1. 변경 파일
==================================================
Core/CoreActivationPresentation.cs
Core/CoreObject.cs
Core/BossDummyController.cs
Enemies/RewardDropper.cs
Enemies/RewardPickup.cs
Shop/ShopStructure.cs
Config/BossReward.asset

==================================================
2. 적용된 변경
==================================================
[코어]
- 씬 시작 시 Animator를 Idle 상태로 강제 초기화
- 활성화 애니메이션이 자동 재생되는 문제 방지
- 활성화 후 코어를 숨기지 않고 Spent 상태로 유지
- Spent 상태가 없으면 Activate 마지막 프레임을 정지해서 유지
- 활성화 후 Collider만 비활성화

[보스 코어 조각]
- BossReward.asset에서 CoreShards 1 제거
- 보스 사망 시 CoreObject가 지정한 위치에 코어 조각 물리 드랍
- 일반 해역: 코어 조각 1
- 심부 해역: 코어 조각 2
- 픽업 프리팹이 누락된 경우에만 직접 지급으로 fallback

[월드 자원]
- 상점 구조선 처치 보상 Credits/Scrap을 즉시 지급하지 않고 물리 드랍
- Credits/Scrap/Core/Heal은 RewardPickup 근접 회수 방식 사용
- 재화별 조각 크기 분리
  Credits: 기본 5 단위당 1조각
  Scrap: 기본 1 단위당 1조각
  Core: 기본 1 단위당 1조각
  Tuning Chip: 기본 1 단위당 1조각

[적재량]
- Scrap/Core 픽업 전체가 적재공간에 들어갈 때만 회수
- 적재량이 부족하면 픽업이 플레이어를 따라오지 않음
- 충돌해도 사라지지 않고 필드에 남음
- Credits/Heal/TuningChip은 기존처럼 적재량과 무관하게 회수

[픽업 비주얼]
- 자식 VisualRoot를 연결하면 부유/펄스 연출 가능
- 공용 BubbleRenderer를 연결하면 재화별 색 자동 적용
- 기존 큰 달러/플러스 아이콘도 내부 아이콘으로 축소해 재사용 가능

==================================================
3. 코어 Animator 필수 설정
==================================================
상태:
Entry -> Idle (기본 상태 / 주황색)
Idle -> Activate
Activate -> Spent

Parameters:
Trigger Activate

Idle -> Activate:
Condition: Activate
Has Exit Time: OFF
Transition Duration: 0

Activate -> Spent:
Has Exit Time: ON
Exit Time: 1
Transition Duration: 0
Condition 없음

Spent:
- 9번 방전 스프라이트 1장만 가진 정적 클립
- Idle로 돌아가는 Transition 금지

CoreActivationPresentation:
Force Idle State On Enable = ON
Idle State Name = Idle
Keep Spent Visual After Activation = ON
Spent State Name = Spent
Freeze Last Frame When Spent State Missing = ON
Disable Colliders After Activation = ON
Hide Core Visual After Activation = OFF

CoreObject:
Preserve Spent Core Visual After Activation = ON
Core Shard Reward Point = 코어 중심 또는 별도 자식 Transform

주의:
Animator의 기본 상태가 Activate로 되어 있으면 코드가 Idle로 돌려도 편집기에서 헷갈릴 수 있다.
반드시 Idle 상태를 Set as Layer Default State로 지정한다.

==================================================
4. RewardPickup 프리팹 권장 구조
==================================================
RewardPickup
- Rigidbody2D
- CircleCollider2D
- RewardPickup

└ VisualRoot
   ├ Bubble      (SpriteRenderer: 작은 원형 테두리/글로우)
   └ Icon        (SpriteRenderer: 달러, 스크랩, 코어, 플러스)

RewardPickup 연결:
Sprite Renderer = Icon
Visual Root = VisualRoot
Bubble Renderer = Bubble

권장 Sprite 크기:
- 월드 기준 전체 지름 약 0.28 ~ 0.42
- Core만 약 0.45 ~ 0.55
- 현재 큰 달러/플러스 이미지는 새로 버릴 필요 없음
- VisualRoot 안의 Icon으로 넣고 50~65% 크기로 축소
- Bubble은 공용 원형 Sprite 1장 재사용

권장 Import:
Texture Type = Sprite (2D and UI)
Pixels Per Unit = 프로젝트 공통값
Filter Mode = Point
Compression = None
Mip Maps = OFF
Pivot = Center

==================================================
5. 자원 비주얼 방향
==================================================
현재 큰 UI 아이콘처럼 보이는 달러/플러스는 월드 오브젝트로는 무겁게 보인다.
새 이미지를 전부 다시 만들기 전에 아래 구조로 먼저 테스트한다.

Credits
- 작은 금색 원형 버블
- 중앙에 축소한 달러/코인 마크

Scrap
- 주황/갈색 원형 버블
- 중앙에 작은 기어 조각/금속 파편

Core
- 다른 자원보다 20~30% 크게
- 주황/백색 외곽광
- 중앙에 다이아몬드 조각

Heal
- 초록 원형 버블
- 중앙 플러스 마크를 작게

즉, Minecraft XP처럼 단순한 작은 원형 픽업 흐름은 적합하다.
단 모든 재화를 완전히 같은 원으로 만들지 말고 색, 내부 심볼, 크기로 구분한다.

==================================================
6. 패시브와 적재량
==================================================
현재 TraitEffectType.CargoCapacityBonus가 이미 구현되어 있다.
다음 패시브가 적재 한도를 증가시킨다.
- 확장 적재실
- 격벽 적재 프레임
- 경량 적재함

권장 규칙:
- 장착된 패시브 자체는 적재량을 소비하지 않음
- 패시브는 기체에 설치된 모듈로 취급
- 패시브가 적재량을 증가시키거나 수확 효율에 영향을 주는 방식 유지

패시브 자체까지 화물 용량을 소모시키면 성장 아이템을 먹을수록 수확 능력이 줄어드는 역보상이 생긴다.
그 제한이 필요하면 Cargo가 아니라 별도의 Passive Slot / Module Slot을 사용한다.

==================================================
7. 테스트 체크리스트
==================================================
[코어]
- 씬 시작 시 Idle 스프라이트가 유지되는가
- E 활성화 전 애니메이션이 재생되지 않는가
- 활성화 후 9번 Spent 스프라이트가 남는가
- 활성화 후 상호작용 Collider가 꺼지는가

[보스]
- 보스 사망 시 방전 코어 위치에 Core 픽업이 생기는가
- 보스 사망 위치에는 Credits/Scrap/Tuning만 생기는가
- 일반 해역 Core 1, 심부 Core 2가 맞는가

[픽업]
- Credits/Scrap/Core/Heal이 필드에 생성되는가
- 플레이어가 가까워지면 흡수되는가
- Cargo가 가득 차면 Scrap/Core가 따라오지 않는가
- Cargo가 부족한 상태에서 픽업과 충돌해도 사라지지 않는가
- Credits/Heal은 Cargo가 가득 차도 정상 회수되는가
