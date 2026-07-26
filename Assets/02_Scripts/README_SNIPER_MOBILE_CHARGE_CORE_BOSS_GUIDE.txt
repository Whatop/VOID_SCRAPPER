VOID SCRAPPER - 스나이퍼 이동 차징 / 코어 스프라이트 애니 / 보스 등장 화면 가이드

1) 이번 수정 내용
- 스나이퍼가 이동 중에도 차징 유지 가능
- 대신 이동 중에는 차징 속도가 느려짐
- 멈춘 상태로 차징하면 카메라의 마우스 방향 오프셋 범위가 더 커짐
  -> 더 멀리 미리 보고 저격 가능

수정 파일
- Player/SniperWeapon.cs
- Core/GungeonStyleCamera2D.cs

기본 추천값
- allowChargeWhileMoving = ON
- movingChargeSpeedMultiplier = 0.6
  -> 이동 중 차징 시간 약 1.67배
- stationaryAimOffsetMultiplier = 1.65
- stationaryMouseDistanceMultiplier = 1.5

--------------------------------------------------
2) 스나이퍼 인스펙터 확인
--------------------------------------------------
대상: Player의 SniperWeapon 컴포넌트

권장 세팅
[Charge]
- Allow Charge While Moving = On
- Moving Charge Speed Multiplier = 0.6
- Cancel Charge On Move = 굳이 안 써도 됨 (이동 허용 ON이면 무시됨)

[Stationary Snipe Camera Assist]
- Stationary Aim Offset Multiplier = 1.65
- Stationary Mouse Distance Multiplier = 1.5

설명
- 이동하면서 좌클릭 유지: 차징 유지, 하지만 천천히 참
- 멈춘 상태에서 좌클릭 유지: 기존보다 빠르게 체감되며, 카메라가 마우스 쪽으로 더 멀리 감
- 즉, '움직이며 안전하게 느리게 차징' / '멈춰서 빠르게 멀리 조준' 둘 다 가능

--------------------------------------------------
3) 코어 활성화 스프라이트 9장 적용법
--------------------------------------------------
지금 코드 구조상 CoreActivationPresentation은 두 방식을 모두 지원함.

A. 가장 쉬운 방식 (추천)
- 9장을 전부 'Activate' 클립 1개로 사용
- Collapse는 코드의 축소/숨김 연출로 처리

설정 순서
1. 코어 오브젝트 하위에 비주얼용 자식 하나 생성
   예) CoreRoot
       └ CoreVisual (SpriteRenderer)

2. CoreVisual의 SpriteRenderer에 기본 코어 스프라이트 설정

3. 프로젝트 창에서 준비한 9장 스프라이트를 선택
   -> Animation 창에 드래그
   -> 새 클립 생성: Core_Activate.anim

4. Animation Clip 설정
- Sample Rate: 10~12 추천
- Loop Time: OFF

5. Animator Controller 생성
- 상태: Idle, Activate
- Trigger 파라미터: Activate
- Idle -> Activate 전환 추가
  - 조건: Trigger = Activate
  - Has Exit Time: OFF
- Activate는 끝까지 재생되도록 설정

6. CoreVisual에 Animator 추가 후 해당 Controller 연결

7. Core 오브젝트의 CoreActivationPresentation 설정
- Animator = CoreVisual의 Animator
- Visual Root = CoreVisual
- Pulse Origin = 코어 중심 Transform
- Tint Renderers = CoreVisual SpriteRenderer
- Renderers To Hide = 코어에 보이는 SpriteRenderer들
- Colliders To Disable = 코어 상호작용 Collider들

8. CoreActivationPresentation의 Trigger 이름 확인
- Activate Trigger = Activate
- Collapse Trigger = 비워둬도 됨

이 방식이면
코어 확대 -> 스프라이트 애니 -> 노란 펄스 -> 코드가 축소/숨김
순서로 바로 동작함.


B. Activate / Collapse를 분리하는 방식
- 1~6프레임: Activate 클립
- 7~9프레임: Collapse 클립

설정 순서
1. Animator 파라미터 두 개 생성
- Activate
- Collapse

2. 상태 구성
- Idle -> Activate (Trigger Activate)
- Activate -> Collapse (Trigger Collapse 또는 코드 호출용)

3. CoreActivationPresentation에서
- Activate Trigger = Activate
- Collapse Trigger = Collapse

이 방식은 스프라이트로 붕괴까지 직접 보여주고 싶을 때 사용.

--------------------------------------------------
4) 코어 활성화 연출 수치 추천
--------------------------------------------------
대상: CoreActivationPresentation / CoreBossIntroSequence

CoreActivationPresentation 권장값
- Pre Pulse Delay = 0.05 ~ 0.08
- Pulse Count = 3
- Pulse Interval = 0.12 ~ 0.15
- Pulse Duration = 0.65 ~ 0.8
- Pulse Start Radius = 0.3 ~ 0.4
- Pulse End Radius = 6 ~ 7
- Collapse Duration = 0.25 ~ 0.35

CoreBossIntroSequence 권장값
- Core Focus Zoom Multiplier = 0.68
- Core Focus Zoom Duration = 0.5 ~ 0.6
- Delay After Core Pulse = 0.1 ~ 0.2
- Wide Zoom Multiplier = 3.2 ~ 3.8
- Zoom Out Duration = 1.2 ~ 1.4
- Boss Reveal Zoom Multiplier = 0.78 ~ 0.86
- Boss Reveal Zoom Duration = 0.45 ~ 0.55
- Boss Health Bar Lead Time = 0.45 ~ 0.6

--------------------------------------------------
5) 보스 등장 화면을 엔터 더 건전식으로 만드는 방법
--------------------------------------------------
현재 프로젝트는 EventTitleDirector + MotionTitleView 구조를 사용 중임.
BossEncounter 전용 프리팹만 따로 만들면 됨.

핵심 포인트
- 보스 등장 타이틀은 일반 타이틀과 다른 프리팹 사용
- 가운데 넓은 패널 + 큰 보스 이름 + 작은 영문/설명 1줄
- 보스 타이틀 종료 직후 체력바가 펼쳐지게 유지

설정 순서
1. 현재 사용 중인 MotionTitleView 프리팹을 복제
   예) BossEncounterTitleView.prefab

2. UI 레이아웃 구성 추천
- 중앙 가로 패널
- 좌우 장식 라인
- 큰 제목 1줄
- 작은 부제 1줄

권장 텍스트 구조
- Title: 구획 관리자
- Subtitle: SECTOR ADMINISTRATOR
또는
- Subtitle: 봉쇄 구역 제어 기체

3. EventTitleDirector 오브젝트 선택
- Title Prefab Overrides 리스트에 새 원소 추가
- Type = BossEncounter
- Prefab = BossEncounterTitleView

4. CoreBossIntroSequence 설정
- Boss Title = 보스 이름
- Boss Subtitle = 영문명 또는 한 줄 설명
- Boss Title Wait = 1.0 ~ 1.2

5. 타이밍 추천
- 보스 중앙 도착
- BossIntroReveal 트리거
- 카메라 보스 쪽으로 살짝 줌인
- BossEncounter 타이틀 재생
- 타이틀 종료 후 BossHealthBarUI.ShowBossAnimated
- 플레이어 조작 복구

--------------------------------------------------
6) 보스 '설명 한 줄'을 넣고 싶을 때
--------------------------------------------------
현재 기본 MotionTitleView는 Title / Subtitle 2줄 구조다.
가장 쉬운 방법은 Subtitle을 설명용으로 쓰는 것이다.

예시
- Title: 구획 관리자
- Subtitle: 잔해 해역 봉쇄 및 침입자 제거 담당

만약
'영문명 + 설명' 둘 다 따로 넣고 싶으면
보스 타이틀 프리팹을 별도로 만들고 MotionTitleView를 확장해야 한다.
이번 패치에는 그 확장 코드는 넣지 않았다.
지금은 2줄 구조로 먼저 쓰는 걸 추천.

--------------------------------------------------
7) 실제 권장 최종 흐름
--------------------------------------------------
코어 E 홀드 완료
-> 카메라 코어 확대
-> 9장 스프라이트 애니 재생
-> 노란 펄스 3회
-> 코어 축소/붕괴
-> 카메라 전장 전체 줌아웃
-> 레이저 관리 기체 4대 진입
-> 봉쇄 벽 형성
-> 보스 화면 밖에서 중앙 진입
-> BossEncounter 타이틀 재생
-> 보스 체력바 펼침
-> 플레이어 조작 복구
-> 전투 시작

--------------------------------------------------
8) 체크리스트
--------------------------------------------------
[스나이퍼]
- 이동 중 좌클릭 유지 시 차징 유지되는가
- 이동 중에는 차징 게이지가 느리게 차는가
- 멈추고 차징하면 카메라가 마우스 쪽으로 더 멀리 가는가

[코어]
- Activate Trigger 호출 시 9장 스프라이트 애니가 재생되는가
- 노란 펄스가 나가는가
- 활성화 후 코어가 축소/숨김 되는가

[보스]
- BossEncounter 전용 프리팹이 사용되는가
- 타이틀 후 체력바가 펼쳐지는가
- 전투 시작 시 플레이어 조작이 자연스럽게 돌아오는가
