# CoplayDev MCP 연결 런북

## 목표

Unity Editor와 외부 AI 클라이언트를 로컬 MCP로 연결해,
스크린샷 왕복 없이 console / scene / menu / object 상태를 직접 읽을 수 있게 한다.

## 전제

- 로컬 개발환경 전용
- sandbox/tooling 브랜치 사용
- working tree clean
- repo에 비밀값/개인 설정 커밋 금지
- repo embedded CoplayDev MCP package 기준 버전은 `9.6.8`

## 권장 연결 순서

1. Unity 프로젝트를 연다.
2. community MCP 패키지를 Unity에 설치한다.
3. Unity에서 MCP 로컬 서버를 시작한다.
4. 외부 AI 클라이언트 설정에 `http://localhost:43157/mcp`를 등록한다.
5. 클라이언트가 연결되면 첫 검증 시나리오를 수행한다.

기존 Unity EditorPrefs에 다른 HTTP URL이 남아 있으면 Unity MCP 창에서 `http://127.0.0.1:43157`로 다시 맞춘다.

## Codex 연결 복구 빠른 절차

Codex MCP 호출이 `http://127.0.0.1:43157/mcp` 전송 실패로 시작하면 `43157` 서버부터 확인한다.

```powershell
Get-NetTCPConnection -LocalPort 43157 -State Listen -ErrorAction SilentlyContinue
Get-NetTCPConnection -LocalPort 8090 -State Listen -ErrorAction SilentlyContinue
pwsh -File tools/unity-bridge.ps1 status
```

`43157` 리스너가 없으면 MCP HTTP 서버를 먼저 올린다.

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

`43157`은 떠 있는데 `Unity session not available`이면 Unity MCP 브리지를 다시 붙인다.
Unity main thread를 막지 않기 위해 동기 대기 호출은 쓰지 않는다.

```powershell
pwsh -File tools/unity-bridge.ps1 exec -Dangerous -Code '_ = MCPForUnity.Editor.Services.MCPServiceLocator.Bridge.StartAsync(); return "MCP bridge start scheduled";'
```

검증은 Codex에서 read-only MCP 호출로 한다.

- `manage_tools list_groups`
- `manage_editor telemetry_status`
- `manage_packages ping`

`manage_packages ping`이 `Package manager is available`을 반환하면 복구 완료다.

`8090` connector가 timeout이거나 Unity 프로세스가 `Responding=False`이면 먼저 포커스 복구를 시도한다.

```powershell
pwsh -File tools/focus-unity.ps1
```

그래도 복구되지 않으면 `8090`을 소유한 현재 프로젝트 Unity PID만 종료하고 다시 연다.

```powershell
$targetPid = (Get-NetTCPConnection -LocalPort 8090 -State Listen).OwningProcess
Stop-Process -Id $targetPid -Force
Start-Sleep -Seconds 5
Start-Process "C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe" -ArgumentList @("-projectPath", "A:\projects\game\survival-manager")
pwsh -File tools/wait-unity-ready.ps1 -MaxAttempts 18 -IntervalSeconds 10 -WarmupSeconds 10
```

이후 다시 비동기 브리지 시작 명령을 실행한다.

## 첫 검증 시나리오

1. console 읽기
2. scene hierarchy 조회
3. `SM/Play/*`, `SM/Internal/*` menu item 조회
4. 승인된 menu item 1회 실행
5. 지정 scene load/save
6. `GameBootstrap`, `SceneFlowController`, `TownScreenController` 존재 확인

## 실패 시 복구

1. MCP write 중지
2. `git status`
3. `git restore`
4. 필요 시 branch reset
5. `SM/전체테스트` 또는 `SM/Internal/Recovery/Repair First Playable Scenes` 재실행

## 금지 사항

- `Assets/ThirdParty` 수정
- 대량 asset 삭제
- 무승인 import
- 승인 없는 package 변경
