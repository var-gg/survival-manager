# Codex 앱 Unity MCP 연결 가이드

- 상태: active
- 최종수정일: 2026-05-05
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

## 빠른 복구 런북

Codex에서 `http://127.0.0.1:43157/mcp` 전송 실패, `Unity session not available`, 또는
`Unity plugin session ... disconnected while awaiting command_result`가 나오면 아래 순서로 좁힌다.

### 1. 서버 포트와 Unity 커넥터 포트 확인

```powershell
Get-NetTCPConnection -LocalPort 43157 -State Listen -ErrorAction SilentlyContinue
Get-NetTCPConnection -LocalPort 8090 -State Listen -ErrorAction SilentlyContinue
pwsh -File tools/unity-bridge.ps1 status
```

- `43157` 리스너가 없으면 MCP HTTP 서버가 꺼진 상태다.
- `8090`은 `unity-cli` connector 포트다. 이 포트가 ready이면 Unity Editor 자체는 살아 있다.
- `43157`은 살아 있는데 `manage_packages ping`이 `Unity session not available`을 반환하면 Unity MCP 브리지가 서버에 붙지 못한 상태다.

### 2. MCP HTTP 서버가 없으면 먼저 서버를 올린다

Unity MCP 창의 `Start Local HTTP Server`가 기본 경로다. GUI를 쓰기 어렵다면 아래 CLI fallback을 사용한다.

```powershell
New-Item -ItemType Directory -Path Logs/MCP -Force | Out-Null

Start-Process `
  -FilePath "$env:USERPROFILE\.local\bin\uvx.exe" `
  -ArgumentList @(
    "--from", "mcpforunityserver==9.6.8",
    "mcp-for-unity",
    "--transport", "http",
    "--http-url", "http://127.0.0.1:43157",
    "--project-scoped-tools"
  ) `
  -WorkingDirectory "A:\projects\game\survival-manager" `
  -RedirectStandardOutput "Logs/MCP/mcp-for-unity-http.stdout.log" `
  -RedirectStandardError "Logs/MCP/mcp-for-unity-http.stderr.log" `
  -WindowStyle Hidden
```

서버 기동 확인:

```powershell
Get-NetTCPConnection -LocalPort 43157 -State Listen -ErrorAction SilentlyContinue
Get-Content Logs/MCP/mcp-for-unity-http.stderr.log -Tail 40
```

정상 로그에는 `Uvicorn running on http://127.0.0.1:43157`와
`transport 'http' on http://127.0.0.1:43157/mcp`가 보인다.

### 3. Unity MCP 브리지는 비동기로만 시작한다

서버가 떠 있는데 Unity session이 없으면 Unity Editor 안에서 브리지를 다시 시작한다.
이때 `GetAwaiter().GetResult()`로 기다리면 Unity main thread가 멈출 수 있으므로 사용하지 않는다.

```powershell
pwsh -File tools/unity-bridge.ps1 exec -Dangerous -Code '_ = MCPForUnity.Editor.Services.MCPServiceLocator.Bridge.StartAsync(); return "MCP bridge start scheduled";'
```

몇 초 기다린 뒤 Codex MCP에서 read-only 호출로 검증한다.

- `manage_tools list_groups`
- `manage_editor telemetry_status`
- `manage_packages ping`

정상 기준은 `manage_packages ping`이 `Package manager is available`을 반환하는 것이다.

### 4. Unity connector가 멈췄으면 해당 프로젝트 인스턴스만 복구한다

`pwsh -File tools/unity-bridge.ps1 status`가 timeout이거나 `Get-Process Unity`에서 `Responding=False`이면
먼저 포커스 복구를 시도한다.

```powershell
pwsh -File tools/focus-unity.ps1
```

복구되지 않으면 `8090` 리스너를 소유한 Unity PID만 종료한다. 다른 Unity 프로젝트 인스턴스는 건드리지 않는다.

```powershell
$targetPid = (Get-NetTCPConnection -LocalPort 8090 -State Listen).OwningProcess
Stop-Process -Id $targetPid -Force
Start-Sleep -Seconds 5
Start-Process "C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe" -ArgumentList @("-projectPath", "A:\projects\game\survival-manager")
pwsh -File tools/wait-unity-ready.ps1 -MaxAttempts 18 -IntervalSeconds 10 -WarmupSeconds 10
```

Unity가 ready가 된 뒤 3단계의 비동기 브리지 시작을 다시 수행한다.

### 5. 성공 판정

아래가 모두 통과하면 Codex Unity MCP는 복구된 것으로 본다.

- `43157`에 `python.exe` 리스너가 있다.
- `8090`에 현재 프로젝트 Unity 리스너가 있고 `tools/unity-bridge.ps1 status`가 `ready`다.
- MCP 서버 로그에 `Plugin registered: survival-manager`가 찍힌다.
- `manage_tools list_groups`가 tool group 목록을 반환한다.
- `manage_packages ping`이 `success=true`를 반환한다.

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
