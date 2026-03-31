# 작업 계획

## 메타데이터

- 작업명:
- 담당:
- 상태:
- 최종수정일:
- 의존:

## Preflight

- Edit Mode 상태 확인
- 현재 compile error / console blocker 확인
- 대상 asmdef와 의존 방향 확인
- done criterion과 oracle을 status/spec에 먼저 적었는지 확인

## Phase 1 code-only

- 코드 경계 정리
- compile blocker 해결
- asset authoring을 섞지 않고 code path만 닫기

## Phase 2 asset authoring

- 필요한 asset 생성/보정/배치 작업
- batch 범위와 refresh 시점
- Play Mode 금지 작업 확인

## Phase 3 validation

- validator 실행
- targeted tests 실행
- runtime path smoke와 evidence 수집

## rollback / escape hatch

- loop budget 초과 시 중단 기준
- parent task / child task로 split하는 조건

## tool usage plan

- file-first / CLI / MCP 사용 순서
- low-level loop를 high-level capability로 어떻게 대체할지

## loop budget

- compile-fix 허용 횟수
- refresh/read-console 반복 허용 횟수
- blind asset generation 재시도 허용 횟수
