# Editor-free semantic guard plan

## Preflight

- 029 push 이후 clean `main`에서 시작한다.
- `BuildBoundaryGuardFastTests`, `tools/test-harness-lint.ps1`, `docs/TESTING.md`의 현재 guard semantics를 확인한다.

## Phase 1 code/harness

- `LoopDTelemetryAndBalanceTests`에 class-level `BatchOnly` category를 추가한다.
- `BuildBoundaryGuardFastTests`에 EditMode test class category closure guard를 추가한다.
- `test-harness-lint.ps1`에 같은 preflight check를 추가한다.

## Phase 2 docs

- `docs/TESTING.md`와 `AGENTS.md`에 class-level category requirement를 명확히 적는다.
- task status에 guard impact와 validation result를 남긴다.

## Phase 3 validation

- FastUnit, lint, focused boundary guard를 순차 실행한다.
- docs policy/check/smoke를 실행한다.

## rollback / escape hatch

- regex false positive가 생기면 helper/no-test files는 제외하고 test attribute가 있는 class만 검사한다.
- BatchOnly class 승격으로 focused test가 실패하면 status에 backlog로 기록하고 category policy는 유지한다.

## loop budget

- guard false positive retry: 2회
- validation retry: 2회
- docs-check retry: 1회
