# Audio Script Apply Notes

추가 파일:
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

Unity에서 `06_Audio/Resources/AudioEventDatabase.asset`가 정상 임포트되면 별도 인스펙터 연결 없이 대부분 자동으로 동작한다.
