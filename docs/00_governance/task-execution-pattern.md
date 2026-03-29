# Task 실행 문서 패턴

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-29
- 소스오브트루스: `docs/00_governance/task-execution-pattern.md`
- 관련문서:
  - `tasks/_templates/spec.md`
  - `tasks/_templates/plan.md`
  - `tasks/_templates/implement.md`
  - `tasks/_templates/status.md`
  - `docs/00_governance/agent-operating-model.md`
  - `docs/00_governance/discord-handoff-format.md`
- 적용범위: Codex 전용

## 목적

이 문서는 다중 단계 작업을 `tasks/<next-id>_<topic>/` 폴더 아래의 실행 문서로 남기는 패턴을 정의한다.
목표는 세션이 끊겨도 작업 의도, 계획, 상태가 복원되게 만드는 것이다.

## 기본 템플릿 세트

`tasks/_templates/` 아래 기본 템플릿은 다음과 같다.

- `spec.md`
- `plan.md`
- `implement.md`
- `status.md`

모든 작업이 네 파일을 다 요구하지는 않지만, 큰 작업은 필요한 최소 집합을 시작 전에 만든다.

## 문서 역할

### `spec.md`

- 목표
- 비목표
- 제약
- 산출물
- 완료 기준

### `plan.md`

- 마일스톤
- 승인 기준
- 검증 명령
- 중단 조건

### `implement.md`

- 실행 방식
- 구현 범위 제한
- 문서 동시 갱신 규칙
- 테스트/검증 규칙

### `status.md`

- 현재 상태
- 완료 항목
- 보류 항목
- 이슈
- 결정
- 다음 단계

## 필수 적용 시점

아래 작업에는 이 패턴을 강하게 적용한다.

- 다중 세션으로 이어질 가능성이 큰 작업
- 구조/정책/의존 방향을 바꾸는 작업
- 승인 지점이나 중단 조건이 있는 작업
- 문서와 구현을 함께 맞춰야 하는 작업

## 폴더 규칙

- 새 task 폴더는 `tasks/<next-id>_<topic>/` 형식을 기본으로 한다.
- `<next-id>`는 기존 숫자 task 폴더 다음 정수 값을 쓴다.
- `<topic>`은 영어 `snake_case` 짧은 주제를 쓴다.
- task 문서는 복붙한 뒤 방치하지 않고 실제 상태에 맞게 즉시 채운다.

## 운영 메모

- `status.md`는 핸드오프 기준 문서다.
- Discord 보고는 `status.md` 요약과 어긋나지 않아야 한다.
- trivial한 작업에는 불필요한 템플릿 복제를 만들지 않는다.
