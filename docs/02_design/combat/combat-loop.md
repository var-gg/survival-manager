# 전투 루프

- 상태: deprecated
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/combat/realtime-simulation-model.md`
- 관련문서:
  - `docs/02_design/combat/battlefield-and-camera.md`
  - `docs/03_architecture/combat-runtime-architecture.md`
  - `docs/04_decisions/adr-0014-grid-deployment-continuous-combat.md`

## 폐기 이유

이 문서는 `resolve once -> replay` 전제를 기준으로 작성되었고, 현재 prototype 구현과 더 이상 맞지 않는다.

## 현재 기준 문서

- 전투 흐름과 fixed-step 규칙은 `realtime-simulation-model.md`를 본다.
- Battle scene 역할은 `docs/03_architecture/combat-runtime-architecture.md`를 본다.
- 전장 표현은 `battlefield-and-camera.md`를 본다.
