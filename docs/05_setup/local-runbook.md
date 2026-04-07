# 로컬 실행 런북

- 상태: active
- 최종수정일: 2026-04-07
- 단계: prototype

## 먼저 실행할 메뉴

- `SM/Setup/Prepare Observer Playable`
- 필요 시 `SM/Setup/Ensure Localization Foundation`

## 빠른 실행 절차

1. Unity editor `6000.4.0f1`로 프로젝트를 연다.
2. `SM/Setup/Prepare Observer Playable`를 실행한다.
3. `Boot.unity`가 자동으로 열렸는지 확인한다.
4. Play 한다.
5. Boot에서 `Start Local Run`을 누른다.
6. 화면 우측 상단 language overlay에서 `ko`/`en` 전환이 되는지 확인한다.

## normal playable lane

1. Town에서 chapter/site 선택, deploy, recruit, save/load를 확인한다.
2. active run이 없으면 `Start Expedition`을 누른다.
3. Expedition에서 current authored site track과 현재 node를 확인한다.
4. battle node면 Battle로 진입한다.
5. Battle 종료 후 `Continue`로 Reward에 간다.
6. Reward 카드 1장 선택 후 `Return Town`으로 복귀한다.
7. Town에서 `Resume Expedition`으로 같은 site track을 재개한다.
8. boss 뒤 extract node는 전투 없이 `Reward -> Town(close)`로 마무리한다.

## 원정 재개 경로

1. Town에서 `Start Expedition`
2. 첫 battle과 Reward를 한 번 완료한 뒤 Town으로 복귀한다.
3. Town에서 chapter/site selector가 잠겼는지 확인한다.
4. 다시 Town에서 `Resume Expedition`을 눌러 진행 중 원정을 재개한다.
5. Quick Battle은 active run이 있을 때 비활성화되어야 한다.

## debug smoke lane

- Town의 `Quick Battle (Smoke)`와 `SM/Quick Battle` 메뉴는 smoke 전용 경로다.
- normal playable acceptance에는 사용하지 않는다.
- direct-scene play fallback과 runtime rebind는 복구 인프라로만 본다.

## 현재 계약 경로

- canonical sample content root: `Assets/Resources/_Game/Content/Definitions/**`
- scene repair source of truth: `SM/Setup/Repair First Playable Scenes`
- one-shot bootstrap: `SM/Setup/Prepare Observer Playable`
- localization foundation source of truth: `SM/Setup/Ensure Localization Foundation`
- default playable content load path: `Resources.LoadAll(...)` only
- editor sweep / file fallback: explicit diagnostic lane only

## 참고 문서

- 최신 playable 상태: [tasks/001_mvp_vertical_slice/status.md](../../tasks/001_mvp_vertical_slice/status.md)
- 현재 알려진 이슈: [docs/06_production/current-known-issues.md](../06_production/current-known-issues.md)
