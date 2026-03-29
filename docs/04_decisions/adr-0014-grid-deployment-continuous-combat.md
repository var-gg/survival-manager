# ADR-0014 Grid Deployment + Continuous Combat 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 결정일: 2026-03-30
- 소스오브트루스: `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md`
- 관련문서:
  - `docs/01_product/vision.md`
  - `docs/02_design/combat/battlefield-and-camera.md`
  - `docs/02_design/combat/realtime-simulation-model.md`
  - `docs/02_design/combat/deployment-and-anchors.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/04_decisions/adr-0006-combat-sim-boundary.md`

## 문맥

기존 prototype 전투는 `BattleResolver`가 결과를 먼저 끝까지 계산하고, Battle scene이 그 결과 이벤트를 replay하는 구조였다.
이 구조는 다음 한계를 만들었다.

- 배치가 사실상 2-row 판정으로 축소됐다.
- 유닛 위치, 방향, cadence가 battle truth에 없다.
- 플레이어는 "자동전투하는 한타를 본다"보다 "이미 정해진 로그를 본다"에 가깝게 느낀다.

반면 제품 비전과 MVP 기준은 readable 3D auto-battle, 4인 배치의 의미, setup-driven outcome을 요구한다.

## 결정

전투 방향을 아래 기준으로 고정한다.

- 배치 phase는 `3x2` 앵커 grid를 사용한다.
- 전투 phase는 off-grid continuous movement를 사용한다.
- 전투 truth는 계속 순수 C# domain인 `SM.Combat`에 둔다.
- Unity는 live simulation step snapshot과 event delta를 소비하는 adapter로 둔다.
- 내부 시뮬레이션은 결정론적 fixed-step 모델을 유지한다.
- 플레이어의 주된 agency는 pre-battle 배치, team posture, unit tactic, synergy, roster, build choice에 둔다.

## 결과

### 기대 효과

- 배치 선택의 의미와 전투 중 살아 움직이는 느낌을 동시에 확보한다.
- EditMode에서 이동, 사거리, retarget, 결정론을 검증할 수 있다.
- Battle scene은 replay 소비기 대신 live observer UI가 된다.

### 감수할 비용

- 상태 모델과 read model이 더 복잡해진다.
- UI와 문서에서 2-row 가정을 모두 걷어내야 한다.
- content authoring이 anchor, posture, spatial stat을 함께 다뤄야 한다.

## 후속

- replay-era 기준 문서는 deprecated로 내리고 새 기준 문서로 교체한다.
- Town/Expedition은 explicit anchor assignment와 team posture selector를 노출한다.
- Battle scene은 pre-resolved playback이 아니라 simulator step loop를 구동한다.
