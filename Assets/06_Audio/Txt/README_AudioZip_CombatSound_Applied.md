# Audio.zip Combat Sound Applied

CelerisLab UI 사운드는 그대로 유지하고, Audio.zip의 OGG 62개 중 일부를 전투/월드 사운드로 배치했다.

## 추가된 주요 영역

- Player: 대쉬, 피격, 사망 파괴음
- Weapons: 기관총, 샷건, 스나이퍼 차징/발사
- Enemies: 적 사격, 차징 예고, 피격, 파괴
- Objects: 수확 오브젝트/운석 피격 및 파괴
- Core/Boss: 코어 활성화, 보스 등장, 레이저, 포격, 차징포, 2페이즈, 보스 사망
- Return: 귀환 비콘 생성, 웜홀 진입
- Shop Combat: 보호막 피격/파괴, 적대 상점 산탄, 보안 드론 호출

## 수정 파일

- 06_Audio/Resources/Audio/SoundEventLibrary.asset
- 02_Scripts/Audio/SoundEventIds.cs
- Player weapon / dash / health scripts
- EnemyAttackController.cs
- EnemyHealth.cs
- HarvestObjectHealth.cs
- MeteorObstacle.cs
- CoreObject.cs
- ReturnBeacon.cs
- WormholePortal.cs
- BossPatternController.cs
- ShopStructure.cs

## 주의

machinegun_fire_loop는 실제 루프가 아니라 기관총 발사 간격에 맞춰 짧게 반복 재생된다.
긴 루프형 엔진음/앰비언스/BGM은 이번 Audio.zip에 적합한 파일이 부족해서 제외했다.
