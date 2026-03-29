# Unity CLI 로컬 Fast Lane 가이드

- 상태: active
- 최종수정일: 2026-03-30
- 소유자: repository
- 소스오브트루스: `docs/05_setup/unity-cli.md`
- 관련문서:
  - `docs/05_setup/unity-mcp.md`
  - `docs/04_decisions/adr-0008-editor-bridge-policy.md`
  - `docs/04_decisions/adr-0011-mcp-adoption-policy.md`
  - `docs/04_decisions/adr-0013-unity-cli-hybrid-lane.md`
  - `prompts/unity-cli-hybrid-ops.md`
  - `tools/unity-bridge.ps1`

## 목적

이 문서는 `unity-cli`를 **로컬 optional fast lane**으로 사용하는 기준을 정리한다.
이 저장소에서 `unity-cli`는 MCP를 제거하거나 대체하는 도구가 아니라, CLI로 빨리 끝나는 read/diagnostic/smoke/report 작업을 줄 세우는 도구다.

기본 원칙은 여전히 file-first다.
코드, 문서, 테스트, repo-tracked YAML은 먼저 파일로 수정하고, Unity bridge는 compile/bootstrap/smoke/report surface로만 사용한다.

## 로컬 설치

Windows PowerShell 기준:

```powershell
irm https://raw.githubusercontent.com/youngwoocho02/unity-cli/master/install.ps1 | iex
```

설치 확인:

```powershell
unity-cli --help
```

이 binary는 로컬 optional tool이다.
CI mandatory dependency로 취급하지 않는다.

## Connector Pin

`Packages/manifest.json`에는 아래 connector pin을 유지한다.

```json
"com.youngwoocho02.unity-cli-connector": "https://github.com/youngwoocho02/unity-cli.git?path=unity-connector#v0.3.5"
```

참고:

- connector `0.3.5`는 Unity `6000.0` 이상을 요구한다.
- connector는 `com.unity.nuget.newtonsoft-json` `3.2.1`을 선언한다.
- 현재 project lock은 `com.unity.nuget.newtonsoft-json` `3.2.2`를 해석하고 있으므로 connector 추가 후 compile로 호환 여부를 같이 확인한다.

## Unity Editor 설정

CLI background 처리 지연을 줄이기 위해 아래 설정을 권장한다.

1. Unity Editor에서 `Edit -> Preferences -> General`로 이동한다.
2. `Interaction Mode`를 `No Throttling`으로 바꾼다.

이 단계는 현재 수동 설정으로 유지한다.

## Repo-Owned Wrapper

기본 실행 진입점은 `tools/unity-bridge.ps1`다.
wrapper는 항상 현재 repo를 `--project`로 고정한다.
Windows에서는 instance discovery가 `--project .`나 백슬래시 경로와 잘 맞지 않을 수 있으므로, wrapper가 절대 경로를 `A:/...` 형식으로 정규화해서 넘긴다.

예시:

```powershell
pwsh -File tools/unity-bridge.ps1 status
pwsh -File tools/unity-bridge.ps1 compile
pwsh -File tools/unity-bridge.ps1 clear-console
pwsh -File tools/unity-bridge.ps1 console
pwsh -File tools/unity-bridge.ps1 bootstrap
pwsh -File tools/unity-bridge.ps1 report-town
pwsh -File tools/unity-bridge.ps1 report-battle
pwsh -File tools/unity-bridge.ps1 smoke-observer
pwsh -File tools/unity-bridge.ps1 test-edit
pwsh -File tools/unity-bridge.ps1 test-play
```

`console` verb는 wrapper 입력을 `-Filter`로 받지만 실제 `unity-cli`에는 `--type`으로 전달한다.
현재 `unity-cli v0.3.5` help 기준 콘솔 필터 flag는 `--type`이다.

## Custom Report Tools

반복 진단은 raw `exec` here-string보다 project-owned custom tool로 승격한다.
현재 기본 report tool은 다음 두 개다.

- `observer_contract_report`: Town/Battle observer contract를 scene YAML 기준으로 요약한다.
- `missing_reference_scan`: first playable scenes와 project-owned prefabs의 missing script를 스캔한다.

이 도구들은 `Assets/_Game/Scripts/Editor/UnityCliTools/**` 아래에 둔다.

## CLI Lane Vs MCP Lane

CLI lane을 먼저 쓰는 경우:

- 한 번의 menu 실행
- console clear/read
- compile
- test
- screenshot
- aggregate report
- one-shot read probe

MCP lane을 유지하는 경우:

- scene/prefab/component/package의 구조적 조작
- typed schema guardrail이 중요한 작업
- 기존 MCP custom tool이 이미 있는 작업
- CLI 결과만으로 충분하지 않은 targeted fallback

운영 규칙:

- MCP tool catalog를 먼저 뒤지지 않는다.
- 이 작업이 CLI one-shot report로 합쳐질 수 있는지 먼저 판단한다.
- 3회 이상 MCP inspect가 필요해 보이면 멈추고 CLI report 또는 custom tool로 압축한다.

## Raw Exec Guardrail

raw `unity-cli exec`는 read-first다.

- broad write authoring 금지
- hidden global mutation 금지
- large scene surgery 금지
- package 변경 경로로 사용 금지

wrapper의 `exec` verb는 기본 차단이며 `-Dangerous` 명시 opt-in이 있어야만 통과한다.
반복되는 probe는 wrapper verb 또는 custom CLI tool로 승격한다.

## 검증 루프

기본 검증 순서는 아래와 같다.

1. file edit
2. `pwsh -File tools/unity-bridge.ps1 compile`
3. `pwsh -File tools/unity-bridge.ps1 clear-console`
4. `pwsh -File tools/unity-bridge.ps1 bootstrap`
5. `pwsh -File tools/unity-bridge.ps1 report-town` 또는 `report-battle`
6. `pwsh -File tools/unity-bridge.ps1 console`
7. `pwsh -File tools/unity-bridge.ps1 test-edit` / `test-play`
8. 그래도 불명확하면 targeted MCP

예전의 `read_console -> execute_menu_item -> hierarchy query` 루프를 기본 경로로 쓰지 않는다.

## Rollback / Uninstall

철회가 필요하면 아래 순서를 따른다.

1. `Packages/manifest.json`에서 connector dependency를 제거한다.
2. 로컬 binary를 삭제한다.
3. `tools/unity-bridge.ps1`, `prompts/unity-cli-hybrid-ops.md`, 관련 custom tool이 더 이상 필요 없으면 제거한다.
4. setup 문서와 `status.md`에 철회 이유를 남긴다.

## Known Risks

- connector는 실제 dependency change다. 문서/ADR 갱신 없이 package만 추가하면 안 된다.
- current project lock과 connector 선언 dependency의 patch version이 다를 수 있다.
- CLI가 빠르다고 해서 broad write automation 기본 경로로 확장하면 정책 위반이 된다.
- Unity Editor throttling이 켜져 있으면 background command 응답이 늦을 수 있다.
