# 작업 구현

## 메타데이터

- 작업명:
- 담당:
- 상태:
- 최종수정일:
- 실행범위:

## Phase log

- Phase 0 preflight에서 확인한 내용
- Phase 1 code-only에서 한 일
- Phase 2 asset authoring에서 한 일
- Phase 3 validation에서 한 일

## deviation

- 원래 계획과 달라진 점
- 범위가 커졌다면 어디서 split했는지

## blockers

- 현재 막는 것
- 다음 세션이 바로 볼 수 있는 확인 포인트

## diagnostics

- console/error digest
- validator/test 핵심 결과
- 관련 문서/도구에서 확인한 사실

## why this loop happened

- compile / refresh / asset / validator loop가 생긴 원인
- 다음 sprint에서 막기 위한 규칙

## 기록 규칙

- 미시 `compile -> refresh -> console` 로그를 그대로 누적하지 않는다.
- phase별 핵심 변화, 예외, 의사결정만 요약한다.
- 문서 인덱스와 task status를 같은 변경 단위에서 갱신한다.
