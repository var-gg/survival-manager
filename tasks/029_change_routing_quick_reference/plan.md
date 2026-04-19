# Change routing quick reference plan

## Preflight

- 028 commit/push 이후 clean `main`에서 시작한다.
- `AGENTS.md`, `docs/index.md`, `docs/03_architecture/index.md`, `dependency-direction.md`의 현재 경계를 확인한다.

## Phase 1 docs

- `AGENTS.md`의 현재 단계 규칙 아래에 compact agent routing table을 추가한다.
- `docs/03_architecture/index.md`에 architecture routing table을 추가한다.
- 표는 combat, reward/passive/loot, narrative spec, authored content, content conversion, session flow, UI/presentation, persistence, docs/harness를 포함한다.

## Phase 2 validation

- docs policy/check/smoke를 실행한다.
- full repo markdownlint가 필요한 경우 별도로 실행한다.

## rollback / escape hatch

- 표가 너무 길어지면 common change types만 남긴다.
- 코드보다 앞서가는 target architecture 문구가 생기면 제거하고 현재 state로 낮춘다.

## loop budget

- docs wording retry: 1회
- docs-check retry: 1회
