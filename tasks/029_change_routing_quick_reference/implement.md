# Change routing quick reference implementation

## Phase 1 summary

- `AGENTS.md`에 agent 운영용 변경 라우팅 표를 추가했다.
- `docs/03_architecture/index.md`에 architecture 진입점용 변경 라우팅 표를 추가했다.
- 표는 combat, core schema, meta reward/progression, narrative spec, authored content, content conversion, session facade, UI/presentation, persistence, docs/harness를 현재 구현 경계 기준으로 분리한다.

## Phase 2 summary

- docs policy, smoke, full markdownlint, targeted docs-check를 통과했다.

## deviation

- 없음.

## blockers

- 없음.

## diagnostics

- routing table이 현재 asmdef 경계를 정확히 반영하는지 확인한다.
- targeted docs-check 결과를 기록한다.

## why this loop happened

026과 028로 editor-free FastUnit 경계가 강해졌지만, agent가 변경 유형별 첫 파일을 잘못 고르면 여전히 `GameSessionState`나 content/bootstrap 쪽으로 빨려 들어갈 수 있다. 029는 변경 라우팅을 문서 첫 화면에 고정해 그 판단 비용을 낮춘다.
