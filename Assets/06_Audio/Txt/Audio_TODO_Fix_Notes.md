# Audio Patch Notes - R/Q/Dash/Button/Loop

적용 내용:

- R Reinforcement 사용음: 정전/전원 꺼짐 느낌 제거, 장비 발동 사운드로 교체.
- PlayerReinforcementController: 장비 사용 완료 시 `ReinforcementUse` 호출.
- 모든 Unity Button: `AutoUIButtonSoundInstaller`가 런타임에 `UISoundButton` 자동 부착.
- Q 레이더: 차징 시작 / 차징 루프 / 차징 취소 / 스캔 펄스 사운드 추가.
- 스나이퍼: 차징 루프 사운드 추가.
- 대쉬: 짧은 후쉬 3종 랜덤으로 교체.
- 이동음: 현실적인 엔진 루프 대신 짧고 작은 `ship_move_puff` 반복음 추가.
- 환경음/BGM: GameState 기준으로 Ambience/Music 루프 자동 재생.

주의:

- BGM은 임시 분위기 루프다. 최종 음악은 나중에 교체하는 게 맞다.
- 이동음이 거슬리면 `PlayerMovementAudioController`의 interval/volumeScale을 조절하거나 컴포넌트를 끄면 된다.
