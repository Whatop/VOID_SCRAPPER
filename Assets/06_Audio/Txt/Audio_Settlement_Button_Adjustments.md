# Audio Settlement/Button Adjustment Patch

변경 내용:

- 정착지 패널 이동/뒤로가기/좌우 이동 버튼의 일반 클릭음 제거.
- 정착지 action 버튼은 성공 시 `ui_unlock`, 비활성/실패 시 `ui_disabled` 사용.
- `UISoundButton`에 비활성 버튼 클릭 사운드 지원 추가.
- `AutoUIButtonSoundInstaller`는 정착지 버튼에 일반 클릭/호버음을 자동 부착하지 않음.
- 탐사 시작 `ui_launch` 사운드를 시스템 가동/출격 느낌으로 교체.
- 플레이어 이동 반복음 `ship_move_puff`를 짧고 가벼운 pluck/bong 계열로 교체하고 빈도/볼륨 감소.

테스트:

1. 정착지에서 보수/특성/뒤로가기/좌우 이동 버튼 클릭 시 일반 클릭음이 나지 않는지 확인.
2. 보수/특성 해금 성공 시 `ui_unlock`이 나는지 확인.
3. 재화 부족/비활성 action 버튼 클릭 시 `ui_disabled`가 나는지 확인.
4. 탐사 시작 버튼 클릭 시 변경된 출격 사운드가 나는지 확인.
5. WASD 이동 시 이전 배기음이 아니라 작고 가벼운 이동음이 낮은 빈도로 나는지 확인.
