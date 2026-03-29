# Task Plan

## Metadata
- Task Name: unity-cli local lane pilot
- Owner: Codex
- Status: in_progress
- Last Updated: 2026-03-30
- Depends On:
  - `docs/00_governance/agent-operating-model.md`
  - `docs/00_governance/task-execution-pattern.md`

## Milestones
1. 현재 상태와 정책 기준을 기록하고 실행 문서를 생성한다.
2. `unity-cli` 로컬 설치와 repo-scoped 기본 검증을 수행한다.
3. connector pin, wrapper, read-oriented report surface를 추가한다.
4. setup/policy/prompt 문서를 갱신하고 rollback path를 정리한다.

## Approval Criteria
- MCP 유지 결정이 문서에 선기록된다.
- package/dependency 변경 이유와 rollback이 문서화된다.
- CLI-first / MCP-typed-lane routing 규칙이 human-facing 문서와 prompt에 모두 반영된다.

## Verification Commands
```powershell
pwsh -File tools/unity-bridge.ps1 status
pwsh -File tools/unity-bridge.ps1 list
pwsh -File tools/unity-bridge.ps1 compile
pwsh -File tools/unity-bridge.ps1 clear-console
pwsh -File tools/unity-bridge.ps1 bootstrap
pwsh -File tools/unity-bridge.ps1 report-town
pwsh -File tools/unity-bridge.ps1 report-battle
pwsh -File tools/unity-bridge.ps1 console
pwsh -File tools/unity-bridge.ps1 smoke-observer
pwsh -File tools/unity-bridge.ps1 test-edit
pwsh -File tools/unity-bridge.ps1 test-play
unity-cli --project "A:/projects/game/survival-manager" missing_reference_scan --scope first_playable
```

## Stop Conditions
- current gameplay task 범위를 흔들어야만 설치가 가능한 경우
- CI mandatory dependency로 번지는 경우
- `Assets/ThirdParty/**` 수정이 필요한 경우
- broad raw exec write가 사실상 기본 경로가 되는 경우
- docs/ADR 갱신 없이 package만 추가하게 되는 경우

## Rollback Steps
1. `Packages/manifest.json`에서 connector dependency를 제거한다.
2. 로컬 `unity-cli` binary를 제거한다.
3. optional setup/prompt/wrapper 문서를 rollback note와 함께 정리한다.
4. 철회 이유와 남은 영향 범위를 `status.md`에 기록한다.
