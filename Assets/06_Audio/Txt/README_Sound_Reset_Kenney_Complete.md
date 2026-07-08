# VOID SCRAPPER Sound Reset - Kenney + CompleteUISFX

적용 내용:
- 기존 CelerisLab/CompleteUISFX UI 사운드는 더 SF 버튼 계열로 재정리.
- 무기, 적, 보스, 코어, 오브젝트 파괴음은 Kenney Sci-fi / Digital / Impact 중심으로 교체.
- `SoundEventLibrary.asset`은 총 82개 이벤트를 전부 새 클립 GUID로 재연결.

적용 방법:
1. Unity 종료.
2. 기존 `06_Audio` 폴더 백업.
3. 이 패치의 `06_Audio`를 덮어쓰기.
4. Unity 실행 후 Reimport.
5. `06_Audio/Resources/Audio/SoundEventLibrary.asset`에서 Missing 여부 확인.

주의:
- `core_interact_loop`, `boss_laser_loop`, `enemy_charger_aim_loop`는 코드상 One-shot 호출이므로 loop 값은 꺼둔 상태다.
- 폴더 안에 과거 사운드가 남아 있으면 헷갈리므로 가능하면 `06_Audio/SFX`를 삭제 후 새 폴더를 넣어라.
