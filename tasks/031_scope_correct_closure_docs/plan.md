# Scope-correct closure docs plan

## Preflight

- `AGENTS.md`, `docs/index.md`, `docs/03_architecture/index.md`, 관련 architecture docs와 `025`~`030` status만 시작 컨텍스트로 읽는다.
- “editor-free boundary closed” 계열 문장이 scope 없이 쓰였는지 확인한다.

## Phase 1 code-only

- 없음. docs-only 작업이다.

## Phase 2 asset authoring

- 없음.

## Phase 3 validation

- docs policy/check/smoke를 실행한다.
- task docs가 요구 섹션을 갖췄는지 확인한다.
- 코드 변경이 함께 포함되므로 `test-batch-fast`와 lint 결과도 최종 status에 연결한다.

## rollback / escape hatch

- docs-check link timeout이 발생하면 targeted changed-docs check로 재시도하고 timeout을 status에 분리 기록한다.
- wording이 너무 넓으면 `pure asmdef`, `FastUnit`, `SM.Unity adapter`, `BatchOnly`, `ManualLoopD` 범주로 쪼갠다.

## tool usage plan

- Markdown 수정은 `apply_patch`로 수행한다.
- 검증은 repo scripts를 사용한다.

## loop budget

- wording correction retry: 2회
- validation retry: 2회
- docs-check retry: 1회
