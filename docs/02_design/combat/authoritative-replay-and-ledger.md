# authoritative replay와 전투 원장

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 소스오브트루스: `docs/02_design/combat/authoritative-replay-and-ledger.md`
- 관련문서:
  - `docs/02_design/meta/pvp-boundary.md`
  - `docs/03_architecture/replay-persistence-and-run-audit.md`
  - `docs/04_decisions/adr-0015-build-compile-audit-pipeline.md`

## 목적

이 문서는 live simulation을 유지하면서도 전투 결과와 보상 이동을 authoritative replay + ledger로 남기는 기준을 정의한다.

## 핵심 규칙

- 전투 중 truth는 `BattleState`다.
- 전투 종료 후에는 `BattleReplayHeader`, `BattleInputSnapshot`, typed event stream, keyframe digest, final state hash를 저장한다.
- reward와 inventory 변화는 append-only ledger에 남긴다.
- observability 로그와 game truth 저장소를 혼동하지 않는다.

## MVP 운영 원칙

- active run snapshot은 저장한다.
- completed match record와 ledger는 JSON fallback에서도 유지한다.
- PvP는 비-MVP지만 async authoritative replay 기준으로 확장 가능해야 한다.
