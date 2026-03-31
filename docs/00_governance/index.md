# 00 거버넌스

## 목적

이 폴더는 저장소 운영 규칙, 문서 유지 기준, 에이전트 작업 절차를 모은다.

## 문서 분리 기준

- 문서 체계와 메타데이터 규칙은 `docs-governance.md`
- 기본 시작 컨텍스트, lifecycle, deprecated suppression 규칙은 `docs-harness.md`
- 겹치는 문서군의 역할 표는 `source-of-truth-matrix.md`
- docs 검증과 eval 초안은 `docs-evals.md`
- 파일명/경로 규칙은 `naming-conventions.md`
- 용어 정의는 `glossary.md`
- Codex 작업 운영 규칙은 `agent-operating-model.md`
- task 문서 운용 방식은 `task-execution-pattern.md`
- 완료 보고 형식은 `discord-handoff-format.md`
- 구조 변경 검수 기준은 `implementation-review-checklist.md`
- localization 운영 기준은 `docs/02_design/ui/localization-policy.md`와 `docs/05_setup/localization-workflow.md`

## 포함 문서

- `docs-governance.md`: 문서 위치, 메타데이터, 링크 갱신의 기준
- `docs-harness.md`: 기본 컨텍스트, trust tier, deprecated lifecycle 기준
- `source-of-truth-matrix.md`: 역할이 겹치는 문서군의 source-of-truth 표
- `docs-evals.md`: docs harness eval 초안과 검증 관점
- `naming-conventions.md`: 문서 파일명과 경로 명명 규칙
- `glossary.md`: 공통 용어와 문서 체계 용어 정의
- `agent-operating-model.md`: Codex 전용 운영 원칙
- `task-execution-pattern.md`: task 폴더와 실행 문서 패턴
- `discord-handoff-format.md`: Codex 완료/핸드오프 보고 형식
- `implementation-review-checklist.md`: 인간과 AI 공통 구조 검수 체크리스트

## 운영 메모

문서 체계나 운영 정책이 durable하게 바뀌면 `docs/04_decisions/` ADR로 남긴다.
central tombstone은 `docs-harness.md`, `docs-governance.md` 같은 기준 문서의 내부 참조로만 다루고 active index에서는 직접 노출하지 않는다.
