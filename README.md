# Survival Manager

## 사람이 기억할 메뉴 2개

- `SM/전체테스트`
  - legacy alias: `SM/Setup/Prepare Observer Playable`
- `SM/전투테스트`
  - legacy alias: `SM/Quick Battle`

## Full Loop 4단계

1. Unity `6000.4.0f1`로 프로젝트를 연다.
2. 메뉴에서 `SM/전체테스트`를 실행한다.
3. `Boot.unity`가 열리면 Play를 누른다.
4. Boot에서 `Start Local Run`을 누른다.

## 두 진입점의 역할

### Full Loop

- canonical local playable lane
- `Boot -> Town -> Expedition -> Battle -> Reward -> Town`
- 저장/복구/진행/리턴까지 포함한 온전한 게임 테스트 경로

### Combat Sandbox

- Battle 씬 직행 pure battle-first lane
- Reward settlement, Town progression, campaign 의미를 들고 가지 않는 전투 실험 경로
- 같은 seed 재생, 새 seed, sandbox 종료, active preset 반복 실행에 집중

### Secondary / Recovery

- Town `Quick Battle (Smoke)`: 현재 Town 상태를 유지한 integration smoke
- `SM/Authoring/Combat Sandbox`: preset library, preview, batch, active handoff sync
- `SM/Recovery/Ensure Localization Foundation`
- `SM/Recovery/Repair First Playable Scenes`
- `SM/Recovery/Validate Canonical Content`

## optional CLI mirrors

- full loop setup: `pwsh -File tools/unity-bridge.ps1 prepare-playable`
- combat sandbox direct entry: `pwsh -File tools/unity-bridge.ps1 quick-battle-smoke`
- release-candidate packet: `pwsh -File tools/pre-art-rc.ps1`

## 지금 실제로 보이는 범위

- `Boot -> Town -> Expedition -> Battle -> Reward -> Town`
- Town operator UI: chapter/site 선택, roster, recruit 카드 4개, squad, deploy preview, save/load, `Start Expedition` / `Resume Expedition`, secondary `Quick Battle (Smoke)`, `Return to Start`
- Expedition operator UI: 5노드 authored track, route 버튼, 현재 위치, node effect, next battle / return town
- Battle observer UI: authored lane summary/log/progress, sandbox lane replay/speed/pause/seed control
- Reward operator UI: 3지선다 카드, gold / item / temporary augment / permanent slot 즉시 반영

## 자동 검증

- EditMode: canonical content, scene integrity, replay builder
- PlayMode: Boot start screen, normal authored expedition loop, Town smoke, combat sandbox direct lane
- docs/structure preflight: `tools/docs-policy-check.ps1`, `tools/smoke-check.ps1`
- release floor wrapper: `tools/pre-art-rc.ps1`가 blocking automated floors와 observer packet을 순차 실행한다.

## 문서 시작점

- 실행 절차: `docs/05_setup/local-runbook.md`
- combat sandbox / Town smoke: `docs/05_setup/quick-battle-smoke.md`
- sandbox authoring 구조: `docs/03_architecture/editor-sandbox-tooling.md`
- scene repair/bootstrap: `docs/05_setup/scene-repair-bootstrap.md`
- operator 체크리스트: `docs/06_production/operator-first-playable-checklist.md`
- 현재 playable 상태: `docs/06_production/first-playable-review.md`
- pre-art release floor: `docs/06_production/pre-art-release-floor.md`
