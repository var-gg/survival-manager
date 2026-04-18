# specialist encounter/tuning/localization 상태

## 메타데이터

- 작업명: specialist encounter/tuning/localization
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19

## Current state

- 020 commit `b3fd73c` 이후 시작했다.
- 020의 `content-validate` 잔여 error 40건, warning 12건은 021에서 닫았다.
- specialist 4종 enemy coverage, authoring schema, Loop C complexity cap, localization tone 정리를 같은 task 범위에서 처리했다.
- localization table 구조는 추가하지 않았고 기존 table/key 안에서 string value만 갱신했다.

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| specialist coverage | 4종 total 2회 이상, elite/boss 1회 이상 | 완료 | `content-validate` issue 0 |
| Loop C tuning | budget/complexity governance error 0 | 완료 | `content-validate` issue 0, `balance-sweep-smoke` exit 0 |
| authoring schema | missing tag/banned pairing error 0 | 완료 | tag/banned pairing issue 0 |
| localization | 기존 key/table 유지, tone 정리 | 완료 | `Content_Characters`, `Content_Story` 기존 table diff |
| validation | `content-validate` error 0 | 완료 | `Logs/content-validation/content-validation-report.json` issue 0 |

## Evidence

- `tasks/020_canonical_resources_audit_green/status.md`
- `Logs/content-validation/content-validation-report.json`
- `Logs/balance-sweep/balance-sweep-report.json`
- `pwsh -File tools/unity-bridge.ps1 test-batch-fast`: failed 0, skipped 3
- `pwsh -File tools/test-harness-lint.ps1`: pass
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`: pass
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`: pass
- targeted `docs-check` for task 021 docs: 4 files, 0 errors
- `TestResults-Batch.xml` focused test runs:
  - `ContentValidationWorkflowTests`: 5/5 pass
  - `LoopCContentGovernanceTests`: 9/9 pass
  - `EncounterAndLootResolutionTests`: 3/3 pass
  - `CharacterAxisLocalizationTests`: 3/3 pass
  - `StoryDirectorServiceTests`: 3/3 pass

## Remaining blockers

- 없음.

## Deferred / debug-only

- docs markdownlint repo-wide cleanup은 022에서 처리한다.
- session service object extraction은 023에서 처리한다.

## Loop budget consumed

- content validation retry: 2/3
- balance smoke retry: 1/2
- localization pass: 1/1
- docs validation retry: 0/1

## Handoff notes

- `SM.Meta` forbidden using guard를 유지한다.
- encounter count를 늘리지 않는다.
- localization table 구조를 새로 만들지 않는다.
- `balance-sweep-smoke`는 exit 0이며 validation error/warning은 0이다. 보고서에는 기존 smoke scenario 성격의 first-cast/synergy outlier가 남지만 실패 조건은 아니다.
