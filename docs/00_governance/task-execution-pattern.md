# Task 실행 문서 패턴

- 상태: active
- 소유자: repository
- 최종수정일: 2026-05-22
- 소스오브트루스: `docs/00_governance/task-execution-pattern.md`
- 관련문서:
  - `PINDOC.md`
  - `docs/00_governance/agent-operating-model.md`
  - `docs/00_governance/discord-handoff-format.md`
  - `tasks/_templates/spec.md`
  - `tasks/_templates/plan.md`
  - `tasks/_templates/implement.md`
  - `tasks/_templates/status.md`
- 적용범위: Codex 전용

## 목적

이 문서는 Codex가 다중 단계 작업의 의도·계획·상태를 어디에 어떻게 남기는지 정의한다.
목표는 세션이 끊겨도 작업 맥락이 복원되게 만드는 것이다.

## 신규 작업은 pindoc Task

신규 다중 단계 작업의 추적은 **pindoc Task artifact**로 한다. `tasks/<id>_<topic>/` 마크다운 폴더를 새로 만들지 않는다.

- 작업 추적이 필요하면 `mcp__pindoc__pindoc_artifact_propose`로 `type: Task` artifact를 만든다. 본문 구조는 `PINDOC.md`의 Template-first propose 규칙대로 `_template_task`를 먼저 읽어 따른다.
- Task 본문에는 Acceptance criteria를 `- [ ]` 체크박스로 둔다. 구현이 끝나면 `pindoc.task.claim_done`으로 체크박스·status·구현 참조를 한 revision에 반영한다.
- Decision/Analysis에서 파생된 실행 단계, 회귀 테스트 누락, Analysis의 open question은 pindoc Task 후보다 (`PINDOC.md`의 Task auto-proposal heuristic).
- chip/병렬 sub-session, 중단/재개, cross-agent 핸드오프 절차는 `PINDOC.md`의 Task lifecycle 절을 따른다.
- 짧은 단일 세션 작업까지 Task artifact로 만들 필요는 없다. solo dev ad-hoc 작업은 git history로 충분하다 (`PINDOC.md` Retroactive policy).

## Legacy — `tasks/<id>_<topic>/` 마크다운 패턴

아래는 pindoc 전환 이전의 `tasks/` 마크다운 실행 문서 패턴이다. 기존 `tasks/` 폴더를 **읽을 때의 참조용**으로만 남긴다. 신규 작업에는 적용하지 않으며, 아래 절들의 "적용한다"류 표현은 당시 패턴 기술이지 신규 작업 지시가 아니다.

## 기본 템플릿 세트

`tasks/_templates/` 아래 기본 템플릿은 다음과 같다.

- `spec.md`
- `plan.md`
- `implement.md`
- `status.md`

모든 작업이 네 파일을 다 요구하지는 않지만, Unity migration, 구조 변경, persistence/asmdef 변경, validator 확장 작업은 네 파일을 모두 만든다.
특히 umbrella task는 parent 문서와 child phase 문서를 같이 둔다.

## 문서 역할

### `spec.md`

- `Goal`
- `Authoritative boundary`
- `In scope`
- `Out of scope`
- `asmdef impact`
- `persistence impact`
- `validator / test oracle`
- `done definition`
- `deferred`

### `plan.md`

- `Preflight`
- `Phase 1 code-only`
- `Phase 2 asset authoring`
- `Phase 3 validation`
- `rollback / escape hatch`
- `tool usage plan`
- `loop budget`

### `implement.md`

- `Phase log`
- `deviation`
- `blockers`
- `diagnostics`
- `why this loop happened`
- 미시 `compile -> refresh -> console` 로그가 아니라 phase별 요약

### `status.md`

- `Current state`
- `Acceptance matrix`
- `Evidence`
- `Remaining blockers`
- `Deferred / debug-only`
- `Loop budget consumed`
- `Handoff notes`

## 필수 적용 시점

아래 작업에는 이 패턴을 강하게 적용한다.

- 다중 세션으로 이어질 가능성이 큰 작업
- 구조/정책/의존 방향을 바꾸는 작업
- 승인 지점이나 중단 조건이 있는 작업
- 문서와 구현을 함께 맞춰야 하는 작업
- validator-first, asmdef preflight, asset batch 규칙이 필요한 Unity 작업

## 폴더 규칙

- 새 task 폴더는 `tasks/<next-id>_<topic>/` 형식을 기본으로 한다.
- `<next-id>`는 기존 숫자 task 폴더 다음 정수 값을 쓴다.
- `<topic>`은 영어 `snake_case` 짧은 주제를 쓴다.
- task 문서는 복붙한 뒤 방치하지 않고 실제 상태에 맞게 즉시 채운다.
- `compile green`만 적고 닫지 않는다. acceptance oracle과 evidence를 함께 남긴다.

## 운영 메모

- `status.md`는 핸드오프 기준 문서다.
- Discord 보고는 `status.md` 요약과 어긋나지 않아야 한다.
- trivial한 작업에는 불필요한 템플릿 복제를 만들지 않는다.
- task가 oversized umbrella로 보이면 parent에서 split plan을 먼저 적고 child phase 문서로 분해한다.
