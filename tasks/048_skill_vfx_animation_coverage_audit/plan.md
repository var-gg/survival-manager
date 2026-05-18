# 048 Skill VFX Animation Coverage Audit Plan

## 메타데이터

- 작업명: 스킬 VFX/애니메이션 커버리지 감사
- 담당: repository
- 상태: active
- 최종수정일: 2026-05-18
- 의존: `tasks/047_compendium_vfx_preview`

## Preflight

- 현재 skill definition 수량과 `Kind`, `DamageType`, `Delivery`, slot 분포를 확인한다.
- 유료 VFX/애니메이션 에셋은 vendor original로 보고 수정하지 않는다.
- 현재 런타임 catalog가 raw asset을 얼마나 노출하는지 분리해서 본다.
- Unity project lock이 있는 경우 batchmode test는 문서 작업의 blocker로 삼지 않는다.

## Phase 1 audit

- 스킬 88개를 표현 수요 기준으로 집계한다.
- Epic Toon FX, TriForge prefab folder를 family 관점으로 분류한다.
- Kevin Iglesias animation folder를 combat gesture 관점으로 분류한다.

## Phase 2 coverage decision

- 표현 가능한 family를 Green, 추가 설계가 필요한 family를 Yellow, bespoke composition 후보를 Red로 나눈다.
- 88개 `VfxHookId`를 88개 prefab one-off로 처리하지 않는 원칙을 확정한다.
- pilot 스킬 6개를 선정해 다음 구현 task의 검수 단위를 만든다.

## Phase 3 validation

- task 문서와 report를 검증한다.
- 코드 변경이 없으므로 `test-batch-fast`는 필수 실행 대상에서 제외한다.
- Unity lock이 지속되면 이전 task와 동일하게 status에 남긴다.

## rollback / escape hatch

- 유료 에셋 경로가 변경되어 inventory가 맞지 않으면 report를 draft로 유지한다.
- raw asset을 실제 prefab mapping으로 붙이는 요청이 커지면 별도 구현 task로 split한다.

## tool usage plan

- `rg`와 PowerShell listing으로 inventory를 수집한다.
- 파일 편집은 `apply_patch`만 사용한다.
- vendor folder에는 쓰기 작업을 하지 않는다.

## loop budget

- compile-fix: 0
- refresh/read-console: 0
- asset authoring retry: 0
