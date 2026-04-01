# 작업 구현

## 메타데이터

- 작업명: Loop A Contract Closure
- 담당: Codex
- 상태: 완료
- 최종수정일: 2026-04-01
- 실행범위: schema seam, combat/runtime migration, validator, tests, docs, debug overlay

## Phase log

- Phase 0 preflight
  - `main`과 `codex/battle-contract-closure`가 같은 HEAD임을 확인했다.
  - 기존 `4-slot + mana + tactic rule` 의존 지점과 `tasks/006_system_deepening_pass`의 deferred 범위를 다시 확인했다.
- Phase 1 code-only
  - shared seam enum/data contract를 `SM.Core.Contracts`로 분리했다.
  - content definition과 compiled model을 6-slot, energy, targeting, summon ownership 계약으로 확장했다.
  - `LoopAContractValidator`를 추가하고 `ContentDefinitionValidator`에 연결했다.
- Phase 2 authoring/docs
  - design/architecture source-of-truth를 Loop A 기준으로 교체했다.
  - 기존 4-slot/mana/cooldown 문서는 새 계약으로 재기록하거나 migration alias 문구로 축소했다.
- Phase 3 validation
  - `compile` green
  - `test-edit` green: `71/71`
  - `docs-policy-check` green
  - `smoke-check` green
  - `docs-check`는 저장소 전역 markdownlint 누적 이슈로 red

## deviation

- `docs-check`는 이번 task 변경분만으로 닫을 수 없는 저장소 전역 lint debt가 커서 exception으로 기록한다.

## blockers

- 없음

## diagnostics

- Loop A acceptance는 새 EditMode test와 기존 battle/service test를 합쳐 닫았다.
- `docs-check` 실패는 `.agents`, `.github`, 기존 `tasks/**`, 기존 `docs/**` 전반의 line-length / heading debt가 원인이다.
- Unity는 빈 `Assets/Tests/EditMode/Temp` 폴더 meta를 재생성할 수 있다.

## why this loop happened

- deep schema/rulebook pass 이후에도 authority, cadence, targeting, summon ownership seam이 runtime truth까지 닫히지 않아 source-of-truth가 분산된 상태로 남았다.
- 이번 loop는 그 seam을 code/doc/validator/test 한 단위로 다시 묶기 위해 생성됐다.

## closure summary

- 코드:
  - 6-slot loadout, energy, lane arbitration, target lock, summon mirrored credit/inheritance/cap/despawn, validator, read model 확장
- 문서:
  - `authority-matrix.md`
  - `resource-cadence-loadout.md`
  - `targeting-and-ai-vocabulary.md`
  - `summon-ownership-and-deployables.md`
  - `v1-exclusions.md`
  - 기존 design/architecture/schema 문서 동기화
- 검증:
  - `pwsh -File tools/unity-bridge.ps1 compile`
  - `pwsh -File tools/unity-bridge.ps1 test-edit`
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
