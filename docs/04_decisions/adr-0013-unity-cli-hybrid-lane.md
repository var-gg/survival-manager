# ADR-0013 Unity CLI Hybrid Lane 채택

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-30
- 결정일: 2026-03-30
- 소스오브트루스: `docs/04_decisions/adr-0013-unity-cli-hybrid-lane.md`
- 관련문서:
  - `docs/05_setup/unity-cli.md`
  - `docs/05_setup/unity-mcp.md`
  - `docs/04_decisions/adr-0008-editor-bridge-policy.md`
  - `docs/04_decisions/adr-0011-mcp-adoption-policy.md`
  - `prompts/unity-cli-hybrid-ops.md`

## 문맥

현재 저장소는 editor bridge를 local tooling acceleration 용도로만 다루며, 기본 정책은 MCP 중심의 read-heavy / narrow-write 평가다.
반면 실제 구현 블록의 검증 루프에서는 `menu`, `console`, `compile`, `test`, aggregate report 같은 one-shot 작업을 빠르게 처리할 별도 fast lane이 필요하다.

`unity-cli`는 이 fast lane에 적합하지만, connector package를 추가해야 하므로 단순한 사용법 변경이 아니라 dependency/workflow 변화다.

## 결정

이 저장소는 `unity-cli`를 **로컬 optional hybrid lane**으로 채택한다.

- MCP는 제거하지 않고 유지한다.
- `unity-cli`는 CLI로 빨리 끝나는 read/diagnostic/smoke/report 작업에 우선 사용한다.
- MCP는 scene/prefab/component/package 같은 typed editor mutation과 기존 custom tool 경로에 남긴다.
- raw `unity-cli exec`는 read-first로 제한하고 broad write authoring 기본 경로로 쓰지 않는다.
- 반복 진단은 repo-owned wrapper 또는 project-owned custom CLI tool로 승격한다.
- 이 lane은 gameplay 구현 블록과 분리된 infra/pilot task로 관리한다.

## 결과

### 기대 효과

- MCP tool fishing을 줄인다.
- menu/console/test/report 성격 작업의 왕복 비용을 낮춘다.
- file-first 원칙을 유지한 채 editor verification surface만 가볍게 만든다.

### 감수할 비용

- connector package를 유지해야 한다.
- local optional setup 문서와 rollback path를 계속 관리해야 한다.
- CLI fast lane이 broad write automation으로 확대되지 않도록 운영 규칙을 유지해야 한다.

## 후속

- setup 기준은 `docs/05_setup/unity-cli.md`에 유지한다.
- hybrid routing rules는 `prompts/unity-cli-hybrid-ops.md`에 유지한다.
- wrapper와 custom report tool은 `tools/unity-bridge.ps1`, `Assets/_Game/Scripts/Editor/UnityCliTools/**`에서 관리한다.
