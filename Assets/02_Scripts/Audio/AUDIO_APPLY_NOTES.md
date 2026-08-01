# Audio Script Apply Notes

추가/수정 파일:
- AudioEventDatabase.cs
- SoundEventIds.cs
- AudioManager.cs
- UISoundButton.cs

수정된 주요 호출부:
- GameBootstrap: AudioManager 자동 생성
- SettlementUIController: 패널/건물/특성/출격 버튼 사운드
- ShopTradeUI / ShopStructure: 상점 열림, 구매 성공/실패, 경고, 적대화
- PlayerReinforcementController / ReinforcementPickup / TraitPickup: 장비/특성 획득, 교체, 분해
- RewardPickup: 크레딧/스크랩/코어/회복 획득음
- RunLevelSystem / RunLevelTraitSelectionUI: 레벨업/특성 선택
- RadarPanelAnimator: 레이더 열림/닫힘
- RunResultPanelUI: 정산 완료/계속 버튼

## 공간 사운드 자동 분류

- `AudioManager.Play()`는 2D로 재생한다.
- `AudioManager.PlayAt()`은 기본적으로 거리 감쇠가 있는 월드 사운드로 재생한다.
- UI, 레이더, 획득음, 플레이어 무기/대쉬/피격음은 이벤트 ID 기준으로 자동 2D 처리한다.
- 적, 오브젝트, 보스, 코어, 상점 전투음은 자동 월드 사운드 처리한다.
- 월드 사운드는 최대 거리 밖에서 재생 자체를 취소한다.
- 이벤트별 동시 보이스 제한과 우선순위 기반 보이스 교체가 적용된다.

## 장애물 차폐

1. Unity에 `AudioOccluder` 레이어를 만든다.
2. 운석, 대형 잔해, 상점 본체, 대형 구조물의 Collider2D 오브젝트를 해당 레이어로 지정한다.
3. AudioManager는 해당 레이어를 자동으로 찾아 Raycast 차폐를 적용한다.
4. 해당 레이어가 없으면 거리 감쇠는 유지되고 차폐만 비활성화된다.
5. 장애물 1개는 볼륨 60% / Low Pass 3500Hz, 2개 이상은 볼륨 30% / Low Pass 1500Hz가 기본값이다.

`AudioEventDatabase`의 `Spatial Mode`를 `World2D`로 바꾸면 이벤트별 Min/Max Distance와 차폐 수치를 직접 조정할 수 있다. 기존 에셋은 `Auto` 상태에서 코드 기본 프로필을 사용한다.

Unity에서 `06_Audio/Resources/Audio/SoundEventLibrary.asset`가 정상 임포트되면 별도 인스펙터 연결 없이 동작한다.

## 2026-07 Sound update

- `SoundEventLibrary.asset`의 Event ID를 `01_ui_click` 형식으로 직접 번호화하고 정렬했다.
- 기존 번호 없는 ID는 AudioEventDatabase 별칭 조회로 호환된다.
- `99_music_shop_loop`를 추가하고 `06_Audio/BGM/shop.wav`를 연결했다.
- 상점 UI에서는 환경음을 끄고 상점 음악을 재생한다.
- 정산 결과와 정착지에서는 환경음을 끈다.
- 기체 선택 및 특성 활성 버튼의 activate/deactivate 중복 재생을 방지한다.
- 상세 적용법: `README_SOUND_NUMBERING_SHOP_MUSIC.md`
