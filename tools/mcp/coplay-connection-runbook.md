# CoplayDev MCP 연결 런북

## 목표

Unity Editor와 외부 AI 클라이언트를 로컬 MCP로 연결해,
스크린샷 왕복 없이 console / scene / menu / object 상태를 직접 읽을 수 있게 한다.

## 전제

- 로컬 개발환경 전용
- sandbox/tooling 브랜치 사용
- working tree clean
- repo에 비밀값/개인 설정 커밋 금지

## 권장 연결 순서

1. Unity 프로젝트를 연다.
2. community MCP 패키지를 Unity에 설치한다.
3. Unity에서 MCP 로컬 서버를 시작한다.
4. 외부 AI 클라이언트 설정에 `http://localhost:43157/mcp`를 등록한다.
5. 클라이언트가 연결되면 첫 검증 시나리오를 수행한다.

## 첫 검증 시나리오

1. console 읽기
2. scene hierarchy 조회
3. `SM/Bootstrap/*` menu item 조회
4. 승인된 menu item 1회 실행
5. 지정 scene load/save
6. `GameBootstrap`, `SceneFlowController`, `TownScreenController` 존재 확인

## 실패 시 복구

1. MCP write 중지
2. `git status`
3. `git restore`
4. 필요 시 branch reset
5. `SM/Bootstrap/Prepare First Playable` 재실행

## 금지 사항

- `Assets/ThirdParty` 수정
- 대량 asset 삭제
- 무승인 import
- 승인 없는 package 변경
