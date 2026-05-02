# Codex 앱 Unity MCP 연결 가이드

- 상태: active
- 최종수정일: 2026-05-02
- 소유자: repository
- 소스오브트루스: `docs/05_setup/codex-mcp-setup.md`

## 목적

이 문서는 Survival Manager를 **OpenClaw와 Codex 앱 양쪽에서 같은 Unity Editor MCP 엔드포인트로 다루기 위한 최소 연결 기준**을 정리한다.

목표는 다음과 같다.

- OpenClaw와 Codex가 같은 로컬 Unity MCP를 바라본다.
- repo 안에는 예시와 운영 규칙만 남기고, 실제 사용자별 설정은 로컬 홈 디렉터리에 둔다.
- MCP는 editor state 가속 용도로만 사용하고 런타임 의존성은 만들지 않는다.

## 공통 엔드포인트

이 저장소의 기본 Unity MCP HTTP 엔드포인트는 아래로 고정한다.

- `http://127.0.0.1:43157/mcp`

헬스 체크 기준 URL:

- `http://127.0.0.1:43157/health`

현재 repo의 CoplayDev MCP embedded package는 `9.6.8`이다.
기존 Unity EditorPrefs에 다른 HTTP URL이 남아 있으면 Unity MCP 창에서 위 endpoint로 다시 맞춘다.

## OpenClaw와 Codex 역할 분리

### OpenClaw

OpenClaw는 현재 로컬 bridge 스크립트를 통해 Unity MCP를 연결한다.

- OpenClaw 설정 파일은 `~/.openclaw/openclaw.json`
- Windows 예시 경로는 `%USERPROFILE%\.openclaw\openclaw.json`
- `unityMCP` 서버는 `unity-mcp-ensure-and-bridge.cmd`를 통해 기동된다.
- 이 스크립트는 Unity HTTP MCP 헬스를 확인한 뒤 `mcp-remote` bridge를 붙인다.

즉, OpenClaw 쪽은 **bridge command 기반 연결**이다.

### Codex 앱

Codex 앱은 같은 엔드포인트를 **직접 RMCP HTTP 클라이언트 방식**으로 붙인다.

- Codex 전역 설정 파일은 `~/.codex/config.toml`
- Windows 예시 경로는 `%USERPROFILE%\.codex\config.toml`
- repo 예시 파일은 `.codex/config.toml.example`

필수 설정은 아래 두 항목이다.

```toml
[mcp_servers.unityMCP]
url = "http://127.0.0.1:43157/mcp"

[features]
rmcp_client = true
```

즉, Codex 앱에는 OpenClaw용 bridge 스크립트를 복제하지 않고, 같은 URL만 직접 등록하면 된다.

## 권장 연결 순서

1. Unity에서 Survival Manager 프로젝트를 연다.
2. Unity Editor 쪽 MCP HTTP 서버를 시작한다.
3. `http://127.0.0.1:43157/health`가 응답하는지 확인한다.
4. Codex 앱 전역 설정 `~/.codex/config.toml`에 `unityMCP`를 등록한다.
5. Codex 세션에서 read-only Unity MCP 호출로 연결을 검증한다.

## 빠른 검증 방법

### 로컬 스크립트

PowerShell에서 아래 스크립트로 헬스를 확인한다.

- `tools/mcp/check-unity-mcp.ps1`

### Codex 세션 검증

Codex 세션에서는 먼저 read-only 호출로 붙는지 확인한다.

예:

- tool group 목록 조회
- console 읽기
- active scene 조회

write 작업은 이 단계가 통과한 뒤에만 시작한다.

## 저장소 반영 원칙

- 실제 사용자 홈 설정 파일은 repo에 커밋하지 않는다.
- repo에는 문서와 example 파일만 둔다.
- OpenClaw와 Codex가 **같은 endpoint 정책**을 공유하도록 유지한다.
- 런타임 코드, player build, `Assets/ThirdParty`는 MCP 연결 방식과 분리한다.

## 관련 문서

- `docs/05_setup/unity-mcp.md`
- `docs/05_setup/community-mcp-setup.md`
- `docs/05_setup/mcp-safety-rules.md`
- `docs/06_production/mcp-usage-checklist.md`
