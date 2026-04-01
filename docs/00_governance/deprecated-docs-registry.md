# Deprecated 문서 registry

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/00_governance/deprecated-docs-registry.md`
- 관련문서:
  - `docs/00_governance/docs-harness.md`
  - `docs/00_governance/docs-governance.md`
  - `docs/04_decisions/adr-0017-docs-context-harness.md`

## 목적

이 문서는 active tree에서 제거한 문서의 tombstone만 중앙에서 관리한다.
deprecated 이유를 원본 파일에 영구 축적하지 않고, replacement와 제거 상태를 한 곳에서 추적한다.

## 운영 규칙

- active index에는 이 registry를 직접 링크하지 않는다.
- replacement가 바뀌면 registry와 source-of-truth matrix를 같이 갱신한다.
- `조치`가 `grace`인 항목은 `remove_by` 이전에 archive 또는 remove로 정리한다.

## registry

| 이전경로 | 대체문서 | 결정기록 | 폐기일 | remove_by | 조치 | 이유 |
| --- | --- | --- | --- | --- | --- | --- |
| `docs/02_design/combat/combat-loop.md` | `docs/02_design/combat/realtime-simulation-model.md`; `docs/03_architecture/combat-runtime-architecture.md`; `docs/02_design/combat/battlefield-and-camera.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-03-30 | 2026-03-31 | removed | `resolve once -> replay` 전제를 쓰던 옛 전투 루프 설명을 fixed-step live simulation 기준으로 통합했다. |
| `docs/02_design/combat/formation-and-targeting.md` | `docs/02_design/combat/deployment-and-anchors.md`; `docs/02_design/combat/team-tactics-and-unit-rules.md`; `docs/03_architecture/combat-state-and-event-model.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-03-30 | 2026-03-31 | removed | row 중심 배치/타깃팅 초안을 spatial anchor와 target scoring 기준으로 대체했다. |
| `docs/02_design/combat/tactics-rules.md` | `docs/02_design/combat/team-tactics-and-unit-rules.md`; `docs/03_architecture/combat-content-mapping.md`; `docs/02_design/combat/realtime-simulation-model.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-03-30 | 2026-03-31 | removed | 초안 메모 성격의 전술 규칙을 team posture와 unit rule 기준 문서로 흡수했다. |
| `docs/02_design/combat/explanation-combat-premise.md` | `docs/02_design/combat/battlefield-and-camera.md`; `docs/02_design/combat/realtime-simulation-model.md`; `docs/02_design/combat/combat-readability.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-03-30 | 2026-03-31 | removed | concept framing 메모를 전장, simulation, readability 기준 문서로 분리해 대체했다. |
| `docs/02_design/deck/explanation-deck-premise.md` | `docs/02_design/systems/squad-blueprint-and-build-ownership.md`; `docs/02_design/systems/skills-items-and-passive-boards.md`; `docs/02_design/deck/roster-archetype-launch-scope.md`; `docs/02_design/combat/authoritative-replay-and-ledger.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-03-30 | 2026-03-31 | removed | `deck` framing 문서를 현재 라이브 용어인 squad blueprint, build, loadout 문서군으로 대체했다. |
| `docs/03_architecture/battle-replay-model.md` | `docs/03_architecture/combat-runtime-architecture.md`; `docs/03_architecture/combat-state-and-event-model.md`; `docs/02_design/combat/realtime-simulation-model.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-03-30 | 2026-03-31 | removed | observer replay track 중심 설명을 live simulation step snapshot 구조로 대체했다. |
| `docs/02_design/meta/recruitment-and-reroll.md` | `docs/02_design/meta/recruitment-contract.md`; `docs/02_design/meta/retrain-contract.md`; `docs/02_design/meta/economy-protection-contract.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-04-01 | 2026-04-08 | removed | `3-card recruit + trait reroll` 기준 문서를 Loop B recruit/retrain/economy split 계약으로 교체했다. |
| `docs/02_design/meta/skill-acquisition-and-retrain.md` | `docs/02_design/meta/retrain-contract.md`; `docs/02_design/meta/recruitment-contract.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-04-01 | 2026-04-08 | removed | retrain을 flex-only Echo sink로 재정의하면서 old summary 문서를 제거했다. |
| `docs/02_design/meta/reward-economy.md` | `docs/02_design/meta/economy-protection-contract.md`; `docs/03_architecture/unit-economy-schema.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-04-01 | 2026-04-08 | removed | run economy의 active source-of-truth를 Gold/Echo split 문서로 옮겼다. |
| `docs/02_design/meta/reward-protection-and-acquisition-loop.md` | `docs/02_design/meta/recruitment-contract.md`; `docs/02_design/meta/economy-protection-contract.md`; `docs/02_design/meta/duplicate-handling-contract.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-04-01 | 2026-04-08 | removed | Loop B recovery 장치를 recruit slot guarantee, duplicate Echo conversion, dismiss refund 문서로 분리했다. |
| `docs/02_design/meta/crafting-and-reroll-economy.md` | `docs/02_design/meta/economy-protection-contract.md`; `docs/02_design/meta/retrain-contract.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-04-01 | 2026-04-08 | removed | crafting과 reroll economy 혼합 문서를 Loop B split economy 계약으로 대체했다. |
| `docs/02_design/meta/crafting-currencies-and-sinks.md` | `docs/02_design/meta/economy-protection-contract.md`; `docs/02_design/meta/equipment-family-and-crafting-depth.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-04-01 | 2026-04-08 | removed | crafting sink와 run recovery currency의 source-of-truth를 분리했다. |
| `docs/03_architecture/persistence-schema.md` | `docs/03_architecture/recruit-offer-schema.md`; `docs/03_architecture/unit-economy-schema.md`; `docs/03_architecture/replay-persistence-and-run-audit.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-04-01 | 2026-04-08 | removed | Loop B recruit/retrain/economy save truth를 전용 schema 문서로 분리했다. |
| `docs/03_architecture/drop-resolution-and-ledger-pipeline.md` | `docs/03_architecture/unit-economy-schema.md`; `docs/02_design/meta/economy-protection-contract.md`; `docs/03_architecture/replay-persistence-and-run-audit.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-04-01 | 2026-04-08 | removed | reward ledger 설명에서 recruit/retrain economy truth를 분리했다. |
| `docs/02_design/ui/mvp-debug-ui.md` | `docs/05_setup/recruitment-and-retrain-harness.md`; `docs/03_architecture/combat-harness-and-debug-contract.md` | `docs/04_decisions/adr-0017-docs-context-harness.md` | 2026-04-01 | 2026-04-08 | removed | recruit/retrain debug 경로를 전용 harness 문서로 옮겼다. |
