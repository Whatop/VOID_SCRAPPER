# VOID SCRAPPER - CelerisLab UI Audio Applied

이 06_Audio는 기존 폴더 틀을 유지하고, CelerisLab UI SFX 중 VOID SCRAPPER에 바로 쓸 것만 분류해서 넣은 버전이다.

## 적용 범위
- UI 클릭/호버/뒤로가기/패널 열림/닫힘
- 정착지 보수/업그레이드 성공/실패
- 상점 열림/구매 성공/구매 실패/경고/적대화
- Reinforcement 장착/드랍/획득/사용
- Q 레이더 UI 열림/닫힘/스캔 피드백/실패
- 크레딧/스크랩/코어/회복/경험치 획득
- 귀환 선택/안전 귀환/결과 정산
- 이벤트 시작/완료

## 자동 로드
`06_Audio/Resources/Audio/SoundEventLibrary.asset`을 추가했다.
`SoundManager`가 씬에 없으면 런타임에 자동 생성되고 이 라이브러리를 `Resources.Load("Audio/SoundEventLibrary")`로 읽는다.

## 아직 별도 작업 필요한 것
CelerisLab은 UI 사운드팩이라 아래는 별도 SF/전투 음원이 필요하다.
- 무기 발사음
- 적 피격/파괴
- 플레이어 엔진/대쉬/피격/사망
- 오브젝트 파괴
- 보스 레이저/폭발
- 우주 앰비언스/BGM
