# specialist encounter/tuning/localization 계획

## 메타데이터

- 작업명: specialist encounter/tuning/localization
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19
- 의존: `tasks/020_canonical_resources_audit_green/status.md`

## Preflight

- `content-validation-report.json`의 error/warning을 code별로 분류한다.
- specialist id와 hero id가 Resources authoring truth에 존재하는지 확인한다.
- 020에서 남긴 localization binary diff는 021 범위로 재검토한다.

## Phase 1 code-only

- seed generator가 같은 invalid content를 재생성하지 않도록 필요한 최소 governance 보정을 넣는다.
- 새 runtime public API, 새 `Manager`/`Helper`/`Util`/`Common` 이름은 만들지 않는다.

## Phase 2 asset authoring

- canonical Resources asset을 직접 수정하거나 seed generator를 통해 재생성한다.
- specialist coverage는 기존 24 encounter의 EnemySquad composition 교체/보강으로 처리한다.
- localization은 기존 `Content_Characters`/관련 table 구조 안에서만 수정한다.

## Phase 3 validation

- `content-validate`를 먼저 green으로 만든다.
- `balance-sweep-smoke`로 보수적 tuning 결과를 확인한다.
- focused tests는 validator 결과에 따라 필요한 범위부터 실행한다.
- 기본 fast/lint/docs smoke는 커밋 전 실행한다.

## rollback / escape hatch

- content validation issue가 generator 구조 문제로 커지면 asset-only 수정을 중단하고 generator 보정으로 돌린다.
- balance sweep smoke가 큰 회귀를 보이면 수치 변경을 되돌리고 coverage/localization만 분리한다.

## tool usage plan

- file-first로 YAML/seed generator를 확인한다.
- Unity batchmode 명령은 순차 실행한다.
- scene/prefab/component authoring은 수행하지 않는다.

## loop budget

- content validation retry: 3회
- balance smoke retry: 2회
- localization pass: 1회
- docs validation retry: 1회
