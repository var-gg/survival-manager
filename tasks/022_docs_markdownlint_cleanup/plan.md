# docs markdownlint cleanup 계획

## 메타데이터

- 작업명: docs markdownlint cleanup
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-19
- 의존: `tasks/021_specialist_encounter_tuning_localization/status.md`

## Preflight

- full `docs-check`를 실행해 현재 markdownlint backlog를 파일과 rule별로 분류한다.
- `.markdownlint-cli2.jsonc` rule 완화가 없는지 확인한다.
- active docs와 root docs 외의 package README issue도 full docs-check green 기준에 포함해 처리한다.

## Phase 1 mechanical cleanup

- MD060 table spacing은 markdownlint auto-fix로 먼저 줄인다.
- MD029 ordered list와 같은 deterministic formatting은 의미 보존 범위에서 정리한다.

## Phase 2 manual cleanup

- MD040 code fence language는 문맥에 맞는 `text`, `powershell`, `yaml`, `json`, `csharp` 등을 지정한다.
- MD036 emphasis heading은 실제 heading 또는 list label로 바꾼다.
- MD028 blockquote blank line은 quote block 형식을 유지하면서 빈 quote line으로 바꾼다.
- MD033 inline HTML은 Markdown 표현으로 바꾼다.

## Phase 3 validation

- full `docs-check`를 green까지 반복한다.
- `docs-policy-check`와 `smoke-check`를 다시 실행한다.
- task `status.md`와 `implement.md`에 명령, 결과, 남은 리스크를 기록한다.

## rollback / escape hatch

- 자동 fix가 문서 의미를 바꾸면 해당 파일만 restore하고 수동 patch로 다시 적용한다.
- full docs-check가 link-check timeout으로만 실패하면 markdownlint 결과와 timeout을 분리해 기록한다.

## loop budget

- markdownlint retry: 4회
- docs-check retry: 3회
- docs policy/smoke retry: 1회
