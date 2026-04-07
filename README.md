# Survival Manager

## 반드시 먼저 실행할 메뉴 1개

- `SM/Setup/Prepare Observer Playable`

## 지금 바로 first playable 보는 4단계

1. Unity `6000.4.0f1`로 프로젝트를 연다.
2. 메뉴에서 `SM/Setup/Prepare Observer Playable`를 실행한다.
3. `Boot.unity`가 열리면 Play를 누른다.
4. Boot에서 `Start Local Run`을 누른다.

## normal playable lane

1. `Start Local Run`
2. Town에서 chapter/site 선택, recruit 4장, squad, deploy, save/load를 확인한다.
3. `Start Expedition`
4. Expedition에서 현재 site의 선형 5-node track을 진행한다.
5. Battle 뒤에는 항상 Reward를 거쳐 Town으로 돌아온다.
6. Town에서 `Resume Expedition`으로 같은 authored site track을 재개한다.
7. boss 뒤 extract settlement도 `Reward -> Town(close)`로 마무리한다.

## debug smoke lane

- Town의 `Quick Battle (Smoke)` secondary CTA
- `SM/Quick Battle`
- direct-scene play 시 emergency root/bootstrap
- runtime scene rebind/repair

위 경로는 개발자용 smoke/복구 인프라다. 본선 playable lane으로 취급하지 않는다.

## optional CLI mirrors

- canonical setup fast lane: `pwsh -File tools/unity-bridge.ps1 prepare-playable`
- debug smoke fast lane: `pwsh -File tools/unity-bridge.ps1 quick-battle-smoke`
- release-candidate packet: `pwsh -File tools/pre-art-rc.ps1`

## 지금 실제로 보이는 범위

- `Boot -> Town -> Expedition -> Battle -> Reward -> Town`
- Town operator UI: chapter/site 선택, roster, recruit 카드 4개, squad, deploy preview, save/load, `Start Expedition` / `Resume Expedition`, secondary `Quick Battle (Smoke)`, `Return to Start`
- Expedition operator UI: 5노드 authored track, route 버튼, 현재 위치, node effect, next battle / return town
- Battle observer UI: replay track, primitive actor, world-space HP label/bar, 로그, speed, pause, progress
- Reward operator UI: 3지선다 카드, gold / item / temporary augment / permanent slot 즉시 반영

## 자동 검증

- EditMode: canonical content, scene integrity, replay builder
- PlayMode: Boot start screen, normal authored expedition loop, debug quick battle smoke lane
- docs/structure preflight: `tools/docs-policy-check.ps1`, `tools/smoke-check.ps1`
- release floor wrapper: `tools/pre-art-rc.ps1`가 blocking automated floors와 observer packet을 순차 실행한다.

## 문서 시작점

- 실행 절차: `docs/05_setup/local-runbook.md`
- scene repair/bootstrap: `docs/05_setup/scene-repair-bootstrap.md`
- quick battle smoke: `docs/05_setup/quick-battle-smoke.md`
- operator 체크리스트: `docs/06_production/operator-first-playable-checklist.md`
- 현재 playable 상태: `docs/06_production/first-playable-review.md`
- pre-art release floor: `docs/06_production/pre-art-release-floor.md`
