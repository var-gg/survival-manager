# 배치와 타깃팅

- 상태: deprecated
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/combat/deployment-and-anchors.md`
- 관련문서:
  - `docs/02_design/combat/team-tactics-and-unit-rules.md`
  - `docs/03_architecture/combat-state-and-event-model.md`
  - `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md`

## 폐기 이유

이 문서는 front/back 2-row와 단순 row targeting을 전제로 했고, 현재 spatial target scoring 구조와 맞지 않는다.

## 현재 기준 문서

- 배치 구조는 `deployment-and-anchors.md`를 본다.
- 타깃 선정 규칙은 `team-tactics-and-unit-rules.md`를 본다.
- state/event 모델은 `docs/03_architecture/combat-state-and-event-model.md`를 본다.
