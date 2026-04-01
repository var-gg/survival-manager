# Unity CLI 로컬 Fast Lane 가이드

- 상태: active
- 최종수정일: 2026-04-02
- 소유자: repository
- 소스오브트루스: `docs/05_setup/unity-cli.md`
- 관련문서:
  - `docs/05_setup/unity-mcp.md`
  - `docs/05_setup/unity-long-running-workloads.md`
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
pwsh -File tools/unity-bridge.ps1 status
```

이 binary는 로컬 optional tool이다.
CI mandatory dependency로 취급하지 않는다.
PowerShell 설치 직후 현재 셸의 `PATH`가 갱신되지 않을 수 있으므로, bare `unity-cli`보다 wrapper 확인을 우선한다.

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
또한 local install 경로 fallback, transient connector retry, `compile` / `bootstrap` / `seed-content` / `test-*` 이후 자동 `status` polling을 포함하므로, busy state를 wrapper가 먼저 흡수하는 기본 경로로 쓴다.
`test-*`가 `run_tests sent (connection closed before response)`로 끝나도 wrapper는 ready 복구 뒤 `TestResults.xml`을 읽어 pass/fail을 다시 판단한다.
sample content generation은 이제 `seed-content` 같은 explicit preflight lane에서만 허용한다.
runtime/test 경로는 canonical content가 준비되지 않았을 때 asset rewrite를 하지 않고, preflight command를 안내하는 쪽으로 고정한다.

예시:

```powershell
pwsh -File tools/unity-bridge.ps1 status
pwsh -File tools/unity-bridge.ps1 compile
pwsh -File tools/unity-bridge.ps1 clear-console
pwsh -File tools/unity-bridge.ps1 console
pwsh -File tools/unity-bridge.ps1 bootstrap
pwsh -File tools/unity-bridge.ps1 seed-content
pwsh -File tools/unity-bridge.ps1 report-town
pwsh -File tools/unity-bridge.ps1 report-battle
pwsh -File tools/unity-bridge.ps1 smoke-observer
pwsh -File tools/unity-bridge.ps1 test-edit
pwsh -File tools/unity-bridge.ps1 test-play
pwsh -File tools/unity-bridge.ps1 loopd-slice
pwsh -File tools/unity-bridge.ps1 loopd-purekit
pwsh -File tools/unity-bridge.ps1 loopd-systemic
pwsh -File tools/unity-bridge.ps1 loopd-runlite
pwsh -File tools/unity-bridge.ps1 loopd-smoke
pwsh -File tools/unity-bridge.ps1 loopd-full
```

`console` verb는 wrapper 입력을 `-Filter`로 받지만 실제 `unity-cli`에는 `--type`으로 전달한다.
현재 `unity-cli v0.3.5` help 기준 콘솔 필터 flag는 `--type`이다.
`test-edit`, `test-play`는 `-TestFilter`를 추가로 받아 특정 namespace/class/test만 좁혀서 실행할 수 있다.

wrapper로 충분하지 않을 때만 direct command를 쓴다.
이 경우 `--project .` 대신 절대 경로를 정규화해서 넘긴다.

```powershell
& "$env:LOCALAPPDATA\unity-cli\unity-cli.exe" --project "A:/projects/game/survival-manager" status
```

## 재부팅 / 재연결 복구

OS 재부팅, Unity Editor 재시작, compile 직후 domain reload 이후에는 wrapper 자체가 아니라
connector heartbeat가 잠시 끊긴 것처럼 보일 수 있다.
이때는 아래 순서를 기본 복구 루프로 사용한다.

1. Unity Editor가 실제로 열려 있는지 확인한다.
2. `pwsh -File tools/unity-bridge.ps1 status`를 먼저 다시 실행한다.
3. `status`가 `not responding` 또는 `connection refused`를 내면 10~30초 대기 후 `status`를 재시도한다.
4. 그래도 안 붙으면 Unity Editor에서 project가 완전히 로드됐는지 확인한 뒤 `status`부터 다시 시작한다.
5. `bare unity-cli` 대신 계속 wrapper를 사용한다.

중요한 점:

- connector port는 세션마다 달라질 수 있으므로 숫자를 기준으로 수동 대응하지 않는다.
- 재부팅 직후에는 `PATH` 반영이 꼬일 수 있으므로 `unity-cli` 직호출보다 wrapper를 우선한다.
- compile, bootstrap, test 직후에는 Editor가 잠시 busy state라 `status`가 한두 번 실패할 수 있다.

## wrapper 출력 해석

아래 메시지는 의미를 구분해서 본다.

- `menu sent (connection closed before response)`
  - menu dispatch 자체는 성공했을 수 있다.
  - wrapper는 이 경우 일정 시간 대기 후 `status` polling으로 ready 복구를 먼저 기다린다.
  - bare `unity-cli` 직호출이라면 바로 실패로 단정하지 말고 `status`, `report-town`, `report-battle`, `console`로 후속 상태를 회수한다.
- `run_tests sent (connection closed before response)`
  - test job이 background로 들어갔을 수 있다.
  - wrapper는 먼저 Unity가 다시 ready가 될 때까지 기다린 뒤 `TestResults.xml`을 읽어 pass/fail을 회수한다.
  - 다만 이 fallback은 Unity test artifact가 정상 저장된다는 전제에 의존하므로, 필요 시 `console` 또는 raw artifact를 추가 확인한다.
- `Unity is ready`
  - connector heartbeat는 복구된 상태다.
  - 이후 report/console 명령을 다시 태워도 된다.
- `not responding`, `connection refused`
  - wrapper 자체 고장으로 보기보다 Editor busy 또는 connector 재기동 구간으로 먼저 해석한다.

## Custom Report Tools

반복 진단은 raw `exec` here-string보다 project-owned custom tool로 승격한다.
현재 기본 report tool은 다음 두 개다.

- `observer_contract_report`: Town/Battle observer contract를 scene YAML 기준으로 요약한다.
- `missing_reference_scan`: first playable scenes와 project-owned prefabs의 missing script를 스캔한다.

이 도구들은 `Assets/_Game/Scripts/Editor/UnityCliTools/**` 아래에 둔다.

Loop D처럼 장시간이지만 shardable한 validation은 test runner 대신 custom tool lane으로 분리한다.
현재 기본 도구는 다음을 포함한다.

- `loop_d_balance_report`: first playable slice, PureKit, Systemic, RunLite, full smoke를 shard 단위로 실행한다.

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

observer/report 검증과 test 검증은 같은 lane으로 무조건 이어 붙이지 않는다.
둘 다 필요하면 사이에 `status` 복구를 확인하거나, 가능하면 별도 루프로 나눠서 실행한다.
sample content drift를 고치거나 canonical root를 normalize해야 할 때도 같은 원칙을 적용한다.
즉, seed generation은 test lane 중간에 암묵적으로 일어나지 않으며, 별도 explicit preflight로 먼저 끝내 둔다.

observer/report 기본 검증 순서는 아래와 같다.

1. file edit
2. `pwsh -File tools/unity-bridge.ps1 compile`
3. `pwsh -File tools/unity-bridge.ps1 clear-console`
4. 필요 시 `pwsh -File tools/unity-bridge.ps1 seed-content`
5. `pwsh -File tools/unity-bridge.ps1 bootstrap`
6. `pwsh -File tools/unity-bridge.ps1 report-town` 또는 `report-battle`
7. `pwsh -File tools/unity-bridge.ps1 console`
8. 그래도 불명확하면 targeted MCP

test 기본 검증 순서는 아래와 같다.

1. file edit
2. `pwsh -File tools/unity-bridge.ps1 compile`
3. canonical content readiness가 의심되면 `pwsh -File tools/unity-bridge.ps1 seed-content`
4. 필요 시 `pwsh -File tools/unity-bridge.ps1 status`
5. `pwsh -File tools/unity-bridge.ps1 test-edit -TestFilter <Namespace.Class>` 또는 `test-play -TestFilter <Namespace.Class>`
6. 필요 시 `pwsh -File tools/unity-bridge.ps1 console`
7. 그래도 불명확하면 test artifact 또는 targeted MCP

`compile`, `bootstrap`, `seed-content`, `test-edit`, `test-play`는 wrapper가 ready polling을 내장한다.
즉, 기본 경로에서는 이 명령들이 끝나기 전에 connector recovery를 먼저 기다린다.
`test-*`는 connection이 중간에 끊겨도 fresh `TestResults.xml`이 생기면 wrapper가 결과를 다시 판정한다.
readiness failure를 숨기려고 test/runtime 경로에서 `EnsureCanonicalSampleContent()`를 재도입하지 않는다.

예전의 `read_console -> execute_menu_item -> hierarchy query` 루프를 기본 경로로 쓰지 않는다.

`smoke-observer` one-shot macro가 compile 직후 재연결 타이밍 때문에 불안정하면,
아래처럼 분리 실행을 fallback 표준으로 사용한다.

```powershell
pwsh -File tools/unity-bridge.ps1 status
pwsh -File tools/unity-bridge.ps1 clear-console
pwsh -File tools/unity-bridge.ps1 seed-content
pwsh -File tools/unity-bridge.ps1 bootstrap
pwsh -File tools/unity-bridge.ps1 status
pwsh -File tools/unity-bridge.ps1 report-town
pwsh -File tools/unity-bridge.ps1 report-battle
pwsh -File tools/unity-bridge.ps1 console -Lines 200 -Filter error,warning,log
```

full test도 같은 원칙을 따른다.

- `test-edit`, `test-play`는 병렬로 던지지 않는다.
- default `test-edit`에는 multi-minute balance smoke를 넣지 않는다.
- 장시간 deterministic suite는 `loopd-*` shard verb처럼 manual artifact lane으로 분리한다.
- sample content regenerate가 필요하면 먼저 `seed-content`를 별도 lane으로 실행하고 test lane 안에서 암묵 repair를 기대하지 않는다.
- `test-*`는 observer bootstrap/report 직후의 같은 직렬 파이프라인에 습관적으로 붙이지 않는다.
- wrapper를 쓴다면 test 실행 직후의 connector recovery는 wrapper가 먼저 처리한다.
- wrapper를 쓴다면 `run_tests sent`만 보고 성공으로 간주하지 않고 결과 artifact까지 회수한다.
- bare `unity-cli`를 직접 쓴다면 connector가 끊긴 직후 즉시 재실행하지 말고 `status`가 돌아올 때까지 기다린다.
- latest confirmed pass artifact가 이미 있으면, 새 run 증빙 회수 실패와 테스트 실패를 같은 의미로 취급하지 않는다.

Loop D나 유사한 장시간 suite는 아래처럼 쪼갠다.

```powershell
pwsh -File tools/unity-bridge.ps1 loopd-slice
pwsh -File tools/unity-bridge.ps1 loopd-purekit
pwsh -File tools/unity-bridge.ps1 loopd-systemic
pwsh -File tools/unity-bridge.ps1 loopd-runlite
```

이 lane은 `test-edit` 기본 경로를 대체하지 않는다.
목적은 Unity Test Runner callback에 장시간 workload를 몰아넣지 않는 것이다.

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
- compile / bootstrap / test 직후에는 connector heartbeat가 잠시 끊겨 wrapper가 false negative처럼 보일 수 있다.
