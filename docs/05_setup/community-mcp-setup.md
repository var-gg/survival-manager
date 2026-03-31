# Community MCP 로컬 설정 가이드

- 상태: draft
- 최종수정일: 2026-03-29
- 소유자: repository

## 목적

이 문서는 커뮤니티 MCP(CoplayDev 계열 포함)를 **로컬 개발환경 전용 tooling**으로 연결 준비할 때의 설치/검증/복구 절차를 정리한다.
이 문서의 목적은 런타임 코드와 MCP를 섞지 않고, 안전장치와 운영 기준을 먼저 고정하는 것이다.

## 핵심 원칙

- 설치 대상은 **로컬 개발환경 전용**이다.
- 런타임 코드와 MCP를 섞지 않는다.
- main 브랜치 런타임은 MCP에 의존하지 않는다.
- repo에 비밀값/개인 설정을 커밋하지 않는다.
- 실제 연결 전에는 백업, clean working tree, sandbox branch를 확인한다.

## 설치 범위

설치 또는 설정 파일은 다음 범위에만 둔다.

- 로컬 사용자 환경
- 로컬 에이전트 설정
- `tools/mcp/` 아래의 문서/샘플 파일

다음 범위에는 넣지 않는다.

- gameplay runtime assembly
- production code path
- player build에 포함되는 asset/config

## 우선 연결 후보

현재 우선 연결 후보는 **CoplayDev/unity-mcp** 계열 community MCP다.
조사 기준으로는 아래 구조가 가장 현실적이다.

- Unity Editor 쪽에 MCP for Unity 패키지 설치
- Unity 내부에서 로컬 서버 시작
- 프로젝트 기본 HTTP 엔드포인트: `http://localhost:43157/mcp`
- 외부 AI 클라이언트가 해당 MCP endpoint에 연결

이 저장소에서는 이 경로를 **로컬 tooling 전용**으로만 본다.
즉, Unity 에디터 상태를 읽고 제한적으로 조작하는 연결이지, 런타임 기능 추가가 아니다.

## 권장 연결 방식

1. Unity에 community MCP 패키지를 설치한다.
2. Unity Editor에서 로컬 MCP 서버를 시작한다.
3. 외부 AI 클라이언트에 `http://localhost:43157/mcp`를 연결한다.
4. 첫 검증 시나리오만 수행한다.
5. 이상 없을 때만 좁은 범위 write를 허용한다.

기본적으로 **HTTP localhost 방식**을 우선 권장한다.
이유는 다음과 같다.

- 외부 direct MCP 흐름에 맞다
- 설정이 비교적 단순하다
- loopback-only로 두면 안전 경계가 명확하다
- repo에 민감 설정을 넣지 않아도 된다
- 프로젝트 전용 포트 `43157`로 고정해 다른 로컬 서비스와 충돌을 줄인다

## 비밀값 / 개인 설정 규칙

다음을 repo에 커밋하지 않는다.

- 토큰
- API key
- 개인 endpoint URL
- 로컬 경로가 박힌 사용자 전용 설정
- 개인 계정 식별자
- machine-specific override 파일

필요하다면 다음 방식을 사용한다.

- `.env.local` 또는 사용자 로컬 config
- 샘플 파일은 `*.example` 또는 문서로만 제공
- 실제 값은 각자 로컬에만 둔다

## 연결 전 체크

연결 전에 아래를 모두 만족해야 한다.

1. Unity 프로젝트 백업 또는 복구 가능한 git 커밋 상태 확보
2. working tree clean 확인
3. sandbox 브랜치 사용 확인
4. 승인된 작업 범위 확인

권장 순서:

1. `git status`로 변경사항 확인
2. 필요하면 커밋 또는 stash
3. `tooling/*` 또는 `sandbox/*` 브랜치 생성
4. 오늘 수행할 허용 작업 범위를 메모
5. 그 다음에만 MCP 연결

## 권장 브랜치 전략

- `tooling/mcp-eval-*`
- `sandbox/mcp-*`

main 브랜치에서 바로 연결 검증하지 않는다.

## 첫 연결 후 검증 시나리오

첫 검증은 반드시 read-heavy와 좁은 write를 섞은 아래 시나리오로 진행한다.

### 1. 콘솔 읽기

- Unity console 메시지 읽기
- 현재 compile error / warning 목록 수집
- 특정 에러 메시지 재확인

### 2. scene hierarchy 조회

- 현재 열린 scene 이름 확인
- root object 목록 확인
- 특정 controller 또는 installer 검색

### 3. menu item 목록/실행

- `SM/Bootstrap/*` 관련 menu item 확인
- 승인된 단일 menu 실행
- 실행 결과 console 재확인

### 4. 특정 scene load/save

- 지정된 scene 하나만 load
- scene hierarchy 재확인
- 변경이 있다면 의도된 좁은 범위만 save

### 5. 특정 controller 존재 확인

예:

- `GameBootstrap`
- `SceneFlowController`
- `TownScreenController`
- 현재 작업 대상 controller

## 금지 시나리오

다음은 금지한다.

- `Assets/ThirdParty` 수정
- 대량 asset 삭제
- 무승인 import
- broad scene rewrite
- project-wide object rename/move
- package 추가/업데이트를 승인 없이 수행

## 설치 기록 방식

연결을 시도했으면 아래를 남긴다.

- 사용한 MCP 종류/버전
- 로컬 설치 위치
- 사용한 branch 이름
- 수행한 검증 시나리오
- 결과 및 rollback 필요 여부

이 기록은 문서나 task note에 남기고, 개인 비밀값은 포함하지 않는다.

## 실패 시 복구

문제 발생 시 아래 순서로 복구한다.

1. MCP write 중지
2. `git status`로 변경 파일 확인
3. `git restore`로 개별 파일 복구
4. 필요 시 branch reset
5. Unity scene 상태가 꼬였으면 scene reinstall bootstrap 재실행

프로젝트 기준 bootstrap 예:

- `SM/Bootstrap/Prepare Observer Playable`

## 최소 도입 원칙

처음에는 다음만 준비한다.

- 설치 문서
- 안전 규칙
- 사용 체크리스트
- 로컬 tooling 폴더 안내

처음부터 repo 런타임이나 production workflow에 결합하지 않는다.

## MCP가 없어도 되는 작업

- 일반 C# 코드 수정
- 문서 작성 및 정리
- 설계 문서/ADR 업데이트
- grep 기반 참조 탐색
- 단순 prefab/meta 파일 비교
- 테스트 코드 작성

## MCP가 있으면 압도적으로 빨라지는 작업

- Unity console 에러 읽기와 반복 확인
- scene hierarchy / object 존재 여부 실시간 조사
- menu item 탐색 및 실행
- 특정 scene load/save 검증
- inspector 기반 연결 누락 추적
- 에디터 내부 상태를 외부 에이전트가 빠르게 확인해야 하는 작업
