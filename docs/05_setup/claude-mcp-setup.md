# Claude Code Unity MCP 연결 가이드

- 상태: active
- 최종수정일: 2026-05-07
- 소유자: repository
- 소스오브트루스: `docs/05_setup/claude-mcp-setup.md`
- 관련문서:
  - `docs/05_setup/codex-mcp-setup.md`
  - `docs/05_setup/unity-mcp.md`
  - `docs/05_setup/mcp-safety-rules.md`

## 목적

Claude Code(Anthropic CLI/IDE 클라이언트)가 Survival Manager 저장소에서 Codex 앱과 **같은 로컬 Unity Editor MCP 엔드포인트**를 보도록 등록하는 최소 절차를 정리한다.

Codex 쪽 등록 절차는 `docs/05_setup/codex-mcp-setup.md`를 따르고, 본 문서는 동일 endpoint를 Claude Code 클라이언트에서 다시 한 번 등록하는 과정만 다룬다. Unity 쪽 서버 기동/복구/헬스 체크는 codex-mcp-setup.md의 런북을 그대로 재사용한다.

## 공통 endpoint

저장소 차원의 Unity MCP HTTP endpoint는 codex 쪽과 동일하다.

- 본 endpoint: `http://127.0.0.1:43157/mcp`
- 헬스 체크: `http://127.0.0.1:43157/health`

이 endpoint를 Claude Code도, Codex 앱도 같이 바라본다. 즉 둘 중 어느 쪽이 먼저 mcp-for-unity HTTP 서버를 띄워두면 다른 쪽은 클라이언트 등록만 하면 된다.

## Claude Code 등록 방식 (project-scoped `.mcp.json`)

저장소 루트의 `.mcp.json`이 표준 등록 위치다. Claude Code는 워크스페이스를 열 때 이 파일을 자동 인식한다.

저장소에 들어 있는 설정은 다음과 같다.

```json
{
  "mcpServers": {
    "unityMCP": {
      "type": "http",
      "url": "http://127.0.0.1:43157/mcp"
    }
  }
}
```

이 파일은 git tracked다. 다른 머신에서 같은 저장소를 클론하면 별도 설정 없이 동일 endpoint가 등록된다. Codex 쪽 `~/.codex/config.toml`(사용자별)과 다르게 **저장소 차원에서 공유되는 설정**이라는 점이 차이다.

- Codex 쪽 패턴: `.codex/config.toml.example` (예시) + `~/.codex/config.toml` (실제 적용)
- Claude Code 쪽 패턴: `.mcp.json` (저장소 자체가 적용본)

## 적용 절차

1. `docs/05_setup/codex-mcp-setup.md` 절차로 Unity Editor MCP HTTP 서버를 먼저 올린다. (Unity Editor → MCP 창 → `Start Local HTTP Server` 또는 같은 문서의 CLI fallback)
2. `http://127.0.0.1:43157/health` 응답을 확인한다.
3. 저장소 루트 `.mcp.json`이 위 형식대로 들어 있는지 확인한다.
4. Claude Code 세션을 **재시작**한다. `.mcp.json` 변경은 in-flight 세션에는 반영되지 않는다.
5. 새 세션에서 Claude Code가 `.mcp.json`을 처음 읽으면 trust prompt가 한 번 뜬다. 승인한다.
6. 등록 성공 시 Claude Code 도구 목록에 `mcp__unityMCP__*` 형태의 함수가 등장한다.

## 빠른 검증

Codex와 동일하게 read-only 호출부터 시작한다.

- `mcp__unityMCP__manage_tools list_groups` 또는 동등 함수로 tool group 목록 조회
- `mcp__unityMCP__manage_packages ping`이 `Package manager is available` 반환

write 작업은 위 두 호출이 통과한 뒤에만 시작한다. 본 저장소의 일반적인 Unity write 작업은 `tools/unity-bridge.ps1` wrapper가 1차이고, MCP는 scene/prefab/component 구조 편집 같은 typed guardrail이 중요한 경우에만 보조한다.

## 빠른 복구

Claude Code 세션에서 `mcp__unityMCP__*` 호출이 실패하거나 도구 목록에 안 보이면 다음 순서로 좁힌다.

1. **mcp-for-unity HTTP 서버 자체가 살아 있는지** — `docs/05_setup/codex-mcp-setup.md` "빠른 복구 런북" 1~3단계와 동일하다.
2. **Unity 인스턴스가 응답하는지** — 같은 문서 4단계 (focus-unity, 필요 시 Unity 재시작) 절차.
3. **Claude Code 세션 재시작** — 1·2가 정상인데도 도구가 안 보이면 Claude Code 재시작이 필요하다.

즉, Unity 쪽 복구는 codex-mcp-setup.md를 SoT로 재사용하고, Claude Code 쪽에서는 세션 재시작만 추가로 신경 쓴다.

## CLAUDE.md와의 관계

`CLAUDE.md`의 "MCP 서버 복구" 절에 적힌 `uvx ... mcp-for-unity --transport http --http-url http://127.0.0.1:43157 --project-scoped-tools` 명령은 mcp-for-unity 서버 프로세스를 직접 띄우는 fallback이다. 본 문서의 절차와 모순되지 않으며, 이 명령은 codex-mcp-setup.md 2단계의 GUI 대안에 해당한다.

## 저장소 반영 원칙

- `.mcp.json`은 저장소 루트에 둔다.
- 사용자별 비밀이나 머신별 경로는 `.mcp.json`에 넣지 않는다. 현재는 endpoint URL만 들어 있어 안전하다.
- Codex와 Claude Code가 **같은 endpoint 정책**을 공유하도록 유지한다. endpoint 변경이 필요하면 본 문서와 codex-mcp-setup.md를 같은 작업 단위에서 갱신한다.
- MCP는 editor state 가속 용도로만 사용하고 런타임 의존성은 만들지 않는다.
