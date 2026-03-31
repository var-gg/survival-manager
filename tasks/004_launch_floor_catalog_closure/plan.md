# 작업 계획

## 메타데이터

- 작업명: Launch Floor Catalog Closure
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-03-31
- 의존:
  - `docs/03_architecture/unity-agent-harness-contract.md`
  - `docs/03_architecture/unity-editor-iteration-and-asset-authoring.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/03_architecture/validation-and-acceptance-oracles.md`

## Preflight

- parent 004를 직접 구현 lane으로 쓰지 않고 child phase 하나만 활성화한다.
- 현재 Editor가 Edit Mode인지 확인한다.
- 현재 compile blocker와 console blocker를 캡처한다.
- 선택한 child phase의 asmdef impact와 persistence impact를 다시 읽는다.
- validator / targeted test / runtime smoke oracle을 status에 먼저 적는다.

## Phase 1 code-only

- child phase 하나에 한정해서 code-only 변경을 수행한다.
- code-only phase에서는 asset generation, menu execution, reserialize를
  섞지 않는다.
- phase 03 arena scaffold는 asmdef/persistence ownership 검토가 끝나기
  전까지 code-only 진입을 보류할 수 있다.

## Phase 2 asset authoring

- 필요한 child phase만 asset batch를 수행한다.
- encounter/status/drops나 skill/support tags는 batch scope를 분리한다.
- Play Mode에서는 generator, menu execution, reserialize를 금지한다.

## Phase 3 validation

- 선택한 child phase에 맞는 validator를 먼저 실행한다.
- targeted EditMode/PlayMode tests를 child phase 단위로 실행한다.
- runtime path smoke는 user path가 명확한 child phase에만 수행한다.

## rollback / escape hatch

- compile-fix loop 2회 초과 시 현재 child phase를 멈추고 diagnosis
  summary를 남긴다.
- asset authoring failure가 반복되면 blind retry 대신 authoring
  transaction 문제로 분류한다.
- asmdef boundary 재절단이 필요하면 별도 refactor sprint로 승격한다.

## tool usage plan

- file-first로 task/spec/status를 먼저 갱신한다.
- `ensure_edit_mode -> run_targeted_compile -> batch_generate_content_assets`
  `-> validate_content_contracts -> run_targeted_editmode_tests`
  `-> run_targeted_runtime_smoke` 순서를 기본으로 둔다.
- `Read Console -> Refresh Unity -> Read Console` 같은 low-level 루프는
  금지한다.

## loop budget

- compile-fix: child phase당 최대 2회
- refresh/read-console 반복: child phase당 최대 1회
- blind asset authoring retry: child phase당 최대 1회
- mid-flight asmdef boundary change: 0회

## 운영 메모

- historical 004는 이미 budget을 초과한 상태로 보인다.
- 따라서 새 작업은 parent 004에서 재시도하지 않고 child phase 문서에서
  budget을 새로 부여한다.
