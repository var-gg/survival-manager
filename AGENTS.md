# AGENTS.md

이 문서는 이 저장소에서 작업하는 사람과 에이전트가 공통으로 따를 최소 운영 원칙만 정의한다.

## 저장소 목적

- Unity 기반 게임 프로젝트의 루트형 저장소를 유지한다.
- 현재 목표는 **목각인형 수준 playable vertical slice**를 빠르게 검증하는 것이다.
- 저장소 phase는 `skeleton`이 아니라 `prototype`이다.

## 구현 허용 범위

- 게임 구현 파일: `Assets/_Game/**`
- 테스트 파일: `Assets/Tests/**`
- 문서/운영 파일: `docs/**`, `tools/**`, 기타 루트 메타파일

## 작업 우선순위

1. 실제 구현 상태와 문서를 맞춘다.
2. 문서 목적을 분리한다.
3. 변경 이유를 기록한다.
4. 플레이어블 vertical slice에 직접 기여하는 작업을 우선한다.

## 디렉터리 책임

- `docs/`: 설계, 결정, 운영 문서
- `prompts/`: 자동화 및 에이전트 프롬프트
- `tools/`: 개발 보조 도구
- `Assets/`: Unity 에셋과 게임 코드
- `Packages/`: 패키지 선언 파일
- `ProjectSettings/`: Unity 설정 파일

## 금지 사항

- 루트 구조를 임의로 변경하지 않는다.
- 목적이 다른 내용을 한 문서에 섞지 않는다.
- 캐시, 임시 산출물, 개인 환경 파일을 커밋하지 않는다.
- 구현 범위 밖에 게임 로직을 흩뿌리지 않는다.

## 현재 단계 규칙

- 지속 문서는 한국어로 유지한다.
- 파일명, 코드, API 식별자는 영어를 유지한다.
- 구현 변경 시 관련 인덱스와 기준 문서를 함께 갱신한다.
- 플레이어블 vertical slice 검증을 막지 않는 방향으로 설계/구현/운영 문서를 정리한다.
- 기본은 file-first다.
- Unity 확인, compile, smoke, aggregate report는 `pwsh -File tools/unity-bridge.ps1 <verb>`를 먼저 사용한다.
- scene/prefab/component/package 구조 편집이나 typed guardrail이 중요한 경우에만 MCP를 사용한다.
- trivial inspect 때문에 MCP tool catalog를 먼저 훑지 않는다.

## 문서 하네스 규칙

- `docs/**`, `prompts/**`, `.agents/skills/**`, `tasks/**`, `tools/docs*.ps1`, `tools/smoke-check.ps1`, `.github/workflows/**`를 건드리는 작업이면 먼저 `$docs-maintainer`를 사용한다.
- 문서 구조, 문서 수명주기, deprecated 정리, 언어 정책, index 체계가 바뀌는 작업이면 task 문서를 먼저 만들고 `status.md`를 handoff 기준으로 유지한다.
- 기본 시작 컨텍스트는 `AGENTS.md` -> `docs/index.md` -> 관련 폴더 `index.md` -> 현재 task `status.md`로 제한한다. 모든 Markdown 파일을 한 번에 읽지 않는다.
- `status: deprecated` 문서와 index의 deprecated pointer는 active source로 쓰지 않는다. replacement, ADR, registry를 우선한다.
- 문서를 수정했으면 같은 작업 단위에서 관련 `index.md`, 관련문서 링크, task `status.md`, 검증 스크립트를 같이 갱신한다.
- `docs/**`와 human-facing task/status 보고는 한국어 본문과 한국어 메타데이터를 유지한다. 파일명, 코드, 명령어, API 식별자는 영어를 유지한다.
