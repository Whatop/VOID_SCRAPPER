VOID SCRAPPER - 코어 애니메이션 타이밍 정리 / 보스 후속 Motion 제거

변경 파일
- Core/CoreActivationPresentation.cs
- Temp/CoreBossIntroSequence.cs

1. 수정 내용

CoreActivationPresentation
- Animator의 Activate 상태 길이를 읽어 펄스와 붕괴 시점을 맞춤
- Animator가 정상 동작하면 기존 코드 확대/색상 Fallback을 기본적으로 겹치지 않음
- 9장 스프라이트 애니메이션이 끝나기 전에 코드가 코어를 축소하던 문제를 완화

CoreBossIntroSequence
- 코어 활성화 Motion 타이틀은 유지
- 보스 중앙 진입 후 재생되던 BossEncounter Motion 타이틀 제거
- 보스 줌/등장 애니메이션 직후 체력바가 펼쳐짐

2. 현재 9프레임이 어색한 주요 원인

스크린샷 기준으로 다음 가능성이 큼.

A. 하나의 클립에 활성화와 붕괴가 같이 들어감
- 앞쪽 프레임: 밝아짐/팽창/폭발
- 뒤쪽 프레임: 어두워짐/축소/잔광
- 그런데 코드도 마지막에 별도 축소/숨김을 실행함
- 결과적으로 붕괴가 두 번 반복돼 끊기는 느낌이 남

권장
- Activate 클립에는 활성화/과충전 구간만 사용
- 최종 축소/숨김은 CoreActivationPresentation 코드에 맡김

예시
- Activate: 1~5번 프레임
- 6~9번 프레임이 어두워지는 붕괴 구간이라면 Activate에서 제외

반대로 9장을 전부 쓸 경우
- Hide Core Visual After Activation을 OFF
- CoreObject 쪽에서 최종 비활성화 시점을 별도로 확인해야 함
- 현재 구조에서는 1~5번만 Activate로 쓰는 편이 안전함

B. 프레임 간격이 전부 동일함
에너지 활성화는 같은 속도로 넘어가면 슬라이드쇼처럼 보임.

추천 타이밍 예시 (활성화 5장 기준)
- Frame 1: 0.00
- Frame 2: 0.12
- Frame 3: 0.22
- Frame 4: 0.31
- Frame 5: 0.40
- Frame 5 유지 키: 0.52

즉
- 시작은 조금 유지
- 중간 충전은 점점 빠르게
- 최대 발광 프레임은 잠깐 유지

C. Sprite 캔버스/Pivot이 다름
각 프레임의 보이는 크기나 중심이 달라지면 코어가 튀거나 흔들림.

9장 공통 확인
- 같은 원본 캔버스 크기
- 같은 Pixels Per Unit
- Pivot = Center
- Mesh Type = Full Rect 권장
- Filter Mode = Point
- Compression = None
- Mip Maps = OFF

스프라이트 시트라면
- Automatic Slice보다 Grid By Cell Size 권장
- 각 셀 크기를 동일하게 자를 것

3. Animator 권장 설정

Parameters
- Trigger: Activate
- Trigger: Collapse (별도 붕괴 애니가 없으면 없어도 됨)

State
- Idle
- Activate

Idle -> Activate
- Condition: Activate
- Has Exit Time = OFF
- Transition Duration = 0
- Fixed Duration = 상관없음

Activate Clip
- Loop Time = OFF
- Transform Scale/Color 키를 넣지 말 것
- SpriteRenderer.Sprite만 애니메이션할 것

이유
- 크기/색상/펄스는 CoreActivationPresentation에서 처리하므로
  Animator에서도 동시에 Scale/Color를 건드리면 연출이 겹침

4. CoreActivationPresentation 권장값

Animator Timing Sync
- Synchronize With Activate Animation = ON
- Activate State Name = Activate
- Fallback Activate Animation Duration = 실제 클립 길이
- Pulse Start Normalized Time = 0.45 ~ 0.60
- Allow Fallback Motion With Animator = OFF

Activation Timing
- Pre Pulse Delay = 0 ~ 0.05
- Pulse Hold Duration = 0.10 ~ 0.18
- Collapse Duration = 0.25 ~ 0.32

Fallback Core Motion
- Use Fallback Motion = OFF 권장
  (Animator가 연결된 코어에서는 불필요)
- Hide Core Visual After Activation = ON

Yellow Pulse
- Pulse Count = 2~3
- Pulse Interval = 0.12~0.15
- Pulse Duration = 0.65~0.75

5. 최종 보스 연출 흐름

코어 확대
-> 코어 Activate 스프라이트 애니메이션
-> 피크 시점에 노란 펄스
-> 코어 축소/숨김
-> 코어 활성화 Motion 타이틀
-> 전장 줌아웃
-> 관리 기체/레이저 벽
-> 보스 진입
-> 보스 줌/스케일 연출
-> 별도 BossEncounter Motion 없음
-> 보스 체력바 펼침
-> 전투 시작

6. 먼저 확인할 항목

- Activate Clip의 Loop Time OFF
- Idle -> Activate Transition Duration 0
- CoreActivationPresentation의 Animator가 실제 CoreVisual Animator인지
- Activate State Name이 실제 상태명과 같은지
- Use Fallback Motion OFF
- 6~9번 프레임이 붕괴 프레임이면 Activate Clip에서 제거
