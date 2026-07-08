# Enemy Death Sound Fix

기존 enemy_death가 잔해/우주 쓰레기 충돌음처럼 들려서 일반 적 사망음에 맞는 짧은 금속 파괴 계열로 교체했다.

## 적용 방식

- `enemy_death` 이벤트를 단일 클립에서 4개 랜덤 클립 배열로 변경
- 볼륨 0.62, 피치 랜덤 0.92~1.08
- 기존 `enemy_death.ogg.meta` GUID는 유지해서 참조 안정성 확보

## 사용 클립

- `enemy_death.ogg` ← `impactMetal_heavy_001.ogg` / 0.359s / guid `ddf2dbbf5a304e0e848a331f7cf27712`
- `enemy_death_02.ogg` ← `impactPlate_heavy_003.ogg` / 0.347s / guid `86e522e858be404c9c09f3b2780e6292`
- `enemy_death_03.ogg` ← `impactMetal_medium_000.ogg` / 0.272s / guid `74b199c781974df593e8498b649601c5`
- `enemy_death_04.ogg` ← `impactTin_medium_003.ogg` / 0.215s / guid `7991adee27c54d3e891b8ebbd006e04a`
