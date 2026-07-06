# VOID SCRAPPER Trait/Reinforcement Numbered Assets + Icons

적용 내용:

- TraitDefinition 40개를 사용자 번호표 기준 `번호_ID.asset` 체계로 정리했다.
- ReinforcementDefinition 41개를 사용자 번호표 기준 `번호_ID.asset` 체계로 유지/정리했다.
- WeaponTrait.zip의 PNG 이미지를 Trait / Reinforcement로 분리하고, 각 번호+ID 파일명으로 복사했다.
- 02_Scripts/Config/Icons 아래에 같은 이미지를 넣고, 각 Trait/Reinforcement asset의 `icon` 필드에 연결했다.
- 구형 Trait_* 에셋과 루트 ReinforcementDefinition/01~05 임시 에셋은 최종 ZIP에서 제거했다.

Unity 적용 권장:

1. 기존 프로젝트의 `02_Scripts/Config/TraitDefinition` 폴더를 백업 후 교체.
2. 기존 프로젝트의 `02_Scripts/Config/ReinforcementDefinition` 폴더를 백업 후 교체.
3. `02_Scripts/Config/Icons` 폴더를 추가.
4. Unity에서 reimport 후 Trait/Reinforcement asset의 Icon 필드가 채워졌는지 확인.

주의:

- Unity에 그냥 덮어쓰기만 하면 기존 구형 파일이 남을 수 있다.
- 깔끔하게 하려면 위 TraitDefinition/ReinforcementDefinition 폴더를 먼저 삭제한 뒤 새 폴더를 넣어라.
