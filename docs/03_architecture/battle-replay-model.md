# Battle Replay Model

- 상태: deprecated
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/03_architecture/combat-runtime-architecture.md`
- 관련문서:
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/02_design/combat/realtime-simulation-model.md`
  - `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md`

## 폐기 이유

이 문서는 replay track 기반 observer presentation 계약을 설명했지만, 현재 prototype은 live simulation step snapshot을 직접 소비한다.

## 현재 기준 문서

- 전투 런타임 책임 분리는 `combat-runtime-architecture.md`를 본다.
- snapshot, event, result 모델은 `combat-state-and-event-model.md`를 본다.
- fixed-step live simulation 방향은 `docs/02_design/combat/realtime-simulation-model.md`를 본다.
