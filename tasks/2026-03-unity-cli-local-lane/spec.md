# 작업 명세

## 메타데이터
- 작업명: unity-cli local lane pilot
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-03-31
- 관련경로:
  - `tasks/2026-03-unity-cli-local-lane/`
  - `tools/unity-bridge.ps1`
  - `prompts/unity-cli-hybrid-ops.md`
  - `docs/05_setup/unity-cli.md`
  - `Packages/manifest.json`
- 관련문서:
  - `AGENTS.md`
  - `docs/00_governance/agent-operating-model.md`
  - `docs/00_governance/task-execution-pattern.md`
  - `docs/05_setup/unity-mcp.md`
  - `docs/04_decisions/adr-0008-editor-bridge-policy.md`
  - `docs/04_decisions/adr-0011-mcp-adoption-policy.md`

## 목표
- `unity-cli` local fast lane를 추가한다.
- 현재 사용 중인 MCP를 제거하지 않고 유지한다.
- CLI로 빠르게 끝나는 read/diagnostic/smoke/report 작업을 MCP fishing 없이 처리하는 운영 기준을 남긴다.

## 비목표
- MCP 제거 또는 대체
- CI mandatory integration
- broad editor write automation
- gameplay 구현 블록 범위 확장

## 제약
- 저장소 정책과 prototype 단계 운영 원칙을 따른다.
- `unity-cli`는 local optional lane으로만 문서화한다.
- rollback 가능성을 확보한다.
- `Assets/ThirdParty/**`는 수정하지 않는다.
- human-facing 문서와 작업 보고는 한국어로 유지한다.
- raw `unity-cli exec`는 read-first로 제한한다.

## 산출물
- `tasks/2026-03-unity-cli-local-lane/` 실행 문서 세트
- `docs/05_setup/unity-cli.md` setup/운영 문서
- `tools/unity-bridge.ps1` wrapper
- `prompts/unity-cli-hybrid-ops.md` 운영 프롬프트
- connector package pin과 관련 decision/doc 업데이트
- repo-scoped verification commands와 rollback 절차

## 완료 기준
- 로컬 설치, connector pin, wrapper, prompt, setup/policy 문서가 같은 변경셋에서 정리된다.
- `unity-cli` lane과 MCP lane의 역할 경계가 문서와 prompt에 명확히 남는다.
- repo가 여전히 file-first, sandbox-first, rollback-first 원칙을 유지한다.
- 기본 검증 명령 결과와 남은 리스크가 `status.md`에 기록된다.
