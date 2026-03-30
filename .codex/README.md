# Codex 보조 디렉터리

이 디렉터리는 Survival Manager를 Codex 앱으로 작업할 때 필요한 로컬 설정 예시와 보조 파일을 둔다.

## Unity MCP

이 저장소에서 Codex 앱은 Unity MCP를 **직접 HTTP RMCP 클라이언트 방식**으로 연결한다.
OpenClaw처럼 별도 bridge command를 둘 필요 없이, Codex 전역 설정에 `unityMCP` 서버 URL만 등록하면 된다.

- 전역 설정 위치: `~/.codex/config.toml`
- Windows 예시 경로: `%USERPROFILE%\.codex\config.toml`
- 예시 파일: `.codex/config.toml.example`
- 운영 문서: `docs/05_setup/codex-mcp-setup.md`

현재 기준 엔드포인트는 `http://127.0.0.1:43157/mcp`다.
