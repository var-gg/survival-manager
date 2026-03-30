# MCP 안전 규칙

- 상태: active
- 최종수정일: 2026-03-29
- 소유자: repository

## 목적

이 문서는 Survival Manager에서 커뮤니티 MCP 또는 유사 editor bridge를 사용할 때 반드시 지켜야 할 안전 규칙을 정의한다.

## 기본 규칙

1. 설치 대상은 로컬 개발환경 전용이다.
2. 런타임 코드와 MCP를 섞지 않는다.
3. repo에 비밀값/개인 설정을 커밋하지 않는다.
4. main 브랜치 런타임은 MCP에 의존하지 않는다.
5. MCP는 local dev/tooling acceleration 용도다.

## 연결 전 필수 체크

연결 전에 반드시 아래를 확인한다.

- Unity 프로젝트 백업 또는 clean commit 상태 확보
- clean working tree 확인
- sandbox 브랜치 사용
- 승인된 작업 범위 확인

이 4개 중 하나라도 빠지면 연결/실행을 미룬다.

## 허용 범위

허용 기본값은 다음과 같다.

- console 읽기
- scene hierarchy 조회
- menu item 목록 확인
- 승인된 단일 menu 실행
- 특정 scene load/save
- 특정 controller / object 존재 확인

## 금지 범위

다음은 기본 금지다.

- `Assets/ThirdParty` 수정
- 대량 asset 삭제
- 무승인 import
- 광범위한 scene/object 재작성
- 승인 없는 package install/update/remove
- broad project setting mutation

## write 작업 원칙

- read -> report -> narrow write 순서를 지킨다.
- write 전 대상 파일/scene 범위를 명확히 말할 수 있어야 한다.
- diff로 확인 가능한 변경만 허용한다.
- sandbox 브랜치에서 먼저 수행한다.

## 복구 원칙

실패하거나 상태가 불분명해지면 즉시 아래를 수행한다.

1. MCP 작업 중단
2. `git status` 확인
3. `git restore` 또는 branch reset
4. 필요 시 scene reinstall bootstrap 재실행

## 기록 원칙

매 세션 최소 기록 항목:

- 사용한 MCP 종류
- 브랜치 이름
- 수행 작업 범위
- 변경 파일 또는 scene
- 복구 필요 여부

비밀값/개인 설정은 기록하지 않는다.

## MCP가 없어도 되는 작업

- 일반 코드 작성/리팩터링
- 문서/ADR 작업
- 테스트 작성
- 텍스트 기반 검색 및 구조 정리
- 런타임 설계 검토

## MCP가 있으면 압도적으로 빨라지는 작업

- console 오류 조사
- scene hierarchy 실시간 조회
- menu item 실행 검증
- 특정 scene load/save 반복 확인
- 특정 object/controller 존재 여부 즉시 확인
- 에디터 내부 상태를 외부 에이전트가 확인해야 하는 작업
