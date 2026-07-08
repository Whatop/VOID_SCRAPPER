# Object Break Sound Fix

암석/컨테이너/잔해/파괴된 선체 파괴음을 교체했다.

## 변경 이유
기존 파괴음은 `spaceTrash` 계열 단일 클립이라 잔해가 굴러가는 느낌이 강했고,
컨테이너/암석/잔해가 서로 비슷하게 들렸다.

## 변경 내용
각 파괴 이벤트를 3개 랜덤 클립으로 변경했다.

- object_container_break: 짧은 금속 상자 파손음 3종
- object_meteor_break: 낮고 거친 암석 파쇄음 3종
- object_debris_break: 중형 금속 잔해 파괴음 3종
- object_shipwreck_break: 대형 선체 파괴음 3종

## 라이브러리 세팅
- 컨테이너: 짧고 선명한 금속 파손
- 암석: 저역 중심, 거친 크런치
- 고가치 잔해: 금속 파열 + 파편
- 파괴된 선체: 큰 저역 붕괴 + 금속 파편

Unity에서 `06_Audio/Resources/Audio/SoundEventLibrary.asset`를 Reimport하면 된다.
