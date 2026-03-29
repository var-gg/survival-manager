# Survival Manager

## 반드시 먼저 실행할 메뉴 1개

- `SM/Bootstrap/Prepare Observer Playable`

## 지금 바로 first playable 보는 3단계

1. Unity `6000.4.0f1`로 프로젝트를 연다.
2. 메뉴에서 `SM/Bootstrap/Prepare Observer Playable`를 실행한다.
3. `Boot.unity`가 열리면 Play를 누른다.

## 첫 전투까지 가장 빠른 클릭 순서

1. Town에서 `Quick Battle`
2. Battle replay 종료 후 `Continue`
3. Reward에서 카드 1장 선택 후 `Return Town`

## 지금 실제로 보이는 범위

- `Boot -> Town -> Expedition -> Battle -> Reward -> Town`
- Town operator UI: roster / recruit 카드 3개 / squad / deploy preview / save / load / debug start / quick battle
- Expedition operator UI: 5노드 박스, route 버튼, 현재 위치, node effect, next battle / return town
- Battle observer UI: replay track, primitive actor, world-space HP label/bar, 로그, speed, pause, progress
- Reward operator UI: 3지선다 카드, gold / item / temporary augment / trait reroll / permanent slot 즉시 반영

## 자동 검증

- EditMode: canonical content, scene integrity, replay builder
- PlayMode: Boot -> Town 자동 진입, Town Quick Battle smoke, Battle replay UI root, Reward -> Town 복귀

## 문서 시작점

- 실행 절차: `docs/05_setup/local-runbook.md`
- scene repair/bootstrap: `docs/05_setup/scene-repair-bootstrap.md`
- quick battle smoke: `docs/05_setup/quick-battle-smoke.md`
- operator 체크리스트: `docs/06_production/operator-first-playable-checklist.md`
- 현재 playable 상태: `docs/06_production/first-playable-review.md`
