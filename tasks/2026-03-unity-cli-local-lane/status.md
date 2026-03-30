# Task Status

## Metadata
- Task Name: unity-cli local lane pilot
- Owner: Codex
- Status: in_progress
- Last Updated: 2026-03-30

## Current Status
- `unity-cli` local fast lane pilot 1차 구현과 검증을 완료했다.
- 현재 project Unity version은 `6000.4.0f1`이다.
- 현재 `Packages/manifest.json`과 `Packages/packages-lock.json`에는 `com.coplaydev.unity-mcp`와 `com.youngwoocho02.unity-cli-connector`가 함께 존재한다.
- wrapper, custom report tool, setup/policy/prompt 문서가 같은 변경 단위에서 정리됐다.
- wrapper-first 운용을 강화했고, local install path fallback과 readiness retry를 넣어 transient heartbeat 지연에 더 강하게 만들었다.

## Completed
- 필수 기준 문서와 현재 manifest/Unity version을 확인했다.
- `SM/Bootstrap/Prepare Observer Playable` menu path가 이미 존재함을 확인했다.
- 기존 MCP를 유지하고 제거하지 않는다는 기본 결정을 선기록했다.
- `unity-cli v0.3.5`를 Windows PowerShell 설치 스크립트로 로컬 설치했다.
- connector pin `com.youngwoocho02.unity-cli-connector#v0.3.5`를 manifest와 packages lock에 반영했다.
- `tools/unity-bridge.ps1` wrapper를 추가했고 Windows 경로를 `A:/...` 형식으로 정규화하도록 맞췄다.
- `observer_contract_report`, `missing_reference_scan` custom CLI tool을 `Assets/_Game/Scripts/Editor/UnityCliTools/**` 아래에 추가했다.
- `docs/05_setup/unity-cli.md`, `prompts/unity-cli-hybrid-ops.md`, `docs/04_decisions/adr-0013-unity-cli-hybrid-lane.md`를 추가했다.
- `docs/05_setup/index.md`, `docs/05_setup/unity-mcp.md`, `docs/04_decisions/index.md`, `tools/README.md`, `prompts/README.md`를 갱신했다.
- `pwsh -File tools/unity-bridge.ps1 status`, `list`, `compile`, `bootstrap`, `report-town`, `report-battle`, `console`, `smoke-observer`, `test-edit`, `test-play`를 검증했다.
- `unity-cli --project "A:/projects/game/survival-manager" missing_reference_scan --scope first_playable` 결과 first playable scene missing script는 `0`이었다.
- EditMode test `19/19`, PlayMode test `4/4` 통과를 확인했다.
- 후속 보강으로 `tools/unity-bridge.ps1`가 local `unity-cli.exe` fallback과 readiness retry를 사용하도록 정리했고, prompt와 setup 문서도 wrapper-first로 갱신했다.

## On Hold
- Unity Preferences `Edit -> Preferences -> General -> Interaction Mode -> No Throttling` 수동 적용
- wrapper verb에 `missing_reference_scan`, `screenshot`, `reserialize`를 추가할지 여부는 후속 운영 판단으로 남겨 둠

## Issues
- 사용자 지시의 ADR 경로는 `adr-0005-editor-bridge-policy.md`였지만 실제 저장소 기준 문서는 `adr-0008-editor-bridge-policy.md`다.
- 현재 워크트리에 unrelated 변경이 많아, 기존 사용자 변경을 덮지 않도록 최소 범위 편집이 필요하다.
- `rg.exe` 실행 권한 문제가 있어 탐색은 PowerShell 기본 명령으로 우회 중이다.
- 이 환경에서 `unity-cli --project .` 또는 백슬래시 경로는 instance 매칭에 실패했고, 절대 경로를 `A:/...` 형태로 정규화해야 했다.
- bare `unity-cli` command는 설치 직후 또는 기존 셸에서 `PATH`가 바로 반영되지 않을 수 있다.
- `smoke-observer` 실행 뒤 `Unsolicited response received on idle HTTP channel` 메시지가 간헐적으로 출력되지만 검증 결과 자체는 정상 반환됐다.
- `test-edit`와 `test-play`를 병렬 실행하면 Unity 상태가 엉키므로 순차 실행이 필요하다.

## Decisions
- 현재 사용 중인 MCP는 유지하며 제거 작업은 하지 않는다.
- `unity-cli`는 gameplay 구현 블록 안이 아니라 별도 infra/pilot task로 다룬다.
- `unity-cli`는 local optional fast lane으로만 문서화한다.
- repo 기본 운영 형태는 `file-first + CLI-fast-lane + MCP-typed-lane`으로 기록한다.
- project-scoped wrapper는 항상 정규화된 절대 경로로 `--project`를 넘긴다.
- custom CLI tool은 raw object 대신 `SuccessResponse` 형태로 반환한다.

## Next Steps
- local 개발 환경에서 `No Throttling` 설정을 적용한다.
- 실제 구현 블록 프롬프트에 `prompts/unity-cli-hybrid-ops.md` 스니펫을 재사용한다.
- 필요 시 `missing_reference_scan`과 screenshot/reserialize를 wrapper verb로 승격한다.
