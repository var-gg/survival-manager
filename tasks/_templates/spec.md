# 작업 명세

## 메타데이터

- 작업명:
- 담당:
- 상태:
- 최종수정일:
- 관련경로:
- 관련문서:

## Goal

- 이 task가 닫아야 할 사용자 가치나 구조 변화 한 줄 요약

## Authoritative boundary

- 이번 sprint/task가 닫을 단일 migration axis
- 이번 task가 source-of-truth를 어디로 이동시키는지
- 동시에 닫지 않을 축

## In scope

- 이번 task 안에서 실제로 편집할 코드/asset/doc 범위

## Out of scope

- 다음 sprint로 넘길 것
- compile green만으로 닫지 않을 것

## asmdef impact

- 영향을 받는 asmdef
- 허용 의존/금지 의존 검토 결과
- cycle 위험 메모

## persistence impact

- 저장 모델/포트/record 영향 여부
- `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions` 책임 변화 여부

## validator / test oracle

- 먼저 확장할 validator
- targeted EditMode/PlayMode test
- runtime path smoke

## done definition

- validator, targeted test, runtime path oracle까지 포함한 종료 조건
- evidence를 어디에 남길지

## deferred

- 이번 task에서 일부러 닫지 않는 후속 항목
