# Unity 장시간 작업 런북

- 상태: active
- 최종수정일: 2026-04-09
- 소유자: repository
- 소스오브트루스: `docs/05_setup/unity-long-running-workloads.md`
- 관련문서:
  - `docs/05_setup/unity-cli.md`
  - `docs/05_setup/unity-mcp.md`
  - `docs/03_architecture/unity-agent-harness-contract.md`
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
  - `tools/unity-bridge.ps1`
  - `tasks/013_unity_long_running_workload_lane/status.md`

## 목적

이 문서는 Unity 작업에서 자주 보이는 `Hold on (busy)` 상태창과 `unity-cli connector remained busy` 증상이
언제 구조적 busy인지, 언제 실제 고장인지, 그리고 이 저장소에서 어떤 lane으로 분리해서 다뤄야 하는지 정리한다.

핵심 판단은 아래 한 줄이다.

> 대부분의 장시간 hold는 CLI/MCP transport 고장보다, Unity 메인 스레드 callback에 장시간 workload를 태운 usage 문제다.

## 무엇이 실제로 문제였는가

이 저장소에서 반복된 정체 구간의 공통점은 아래였다.

- `EditMode test`가 `UnityEditor.TestTools.TestRunner.TestRun.TestJobRunner.ExecuteCallback()` 안에서 오래 머무름
- `MenuItem` 경로로 balance/readability/prune runner를 한 번에 실행함
- compile, bootstrap, report, test를 같은 직렬 파이프라인에 습관적으로 이어 붙임

이 경우 Unity Editor는 살아 있어도 main thread heartbeat가 늦어지고, CLI/MCP는 `busy`, `not responding`,
`connection closed before response`처럼 보일 수 있다.

즉, 이것은 대체로 다음 둘의 조합이다.

- 불가피한 Unity 비용: compile, domain reload, asset refresh, test runner 초기화
- 피할 수 있는 usage 비용: 장시간 deterministic suite를 기본 test/menu callback에 태움

## 불가피한 busy와 피할 수 있는 busy

### 불가피한 busy

- `compile` 직후 짧은 domain reload
- `test-play` 직전/직후 scene reload
- `seed-content`, `bootstrap` 직후 asset refresh
- 짧은 `status` 재시도 후 회복되는 connector heartbeat 끊김

이 구간은 몇 초에서 수십 초까지는 정상 범주로 본다.

### 피할 수 있는 busy

- `test-edit` 기본 경로에 multi-minute balance smoke를 넣는 것
- `SM/Internal/Validation/...` 메뉴에서 seed/scenario/artifact sweep를 한 번에 수행하는 것
- Loop D 같은 shardable suite를 하나의 editor callback으로 몰아넣는 것
- `compile -> bootstrap -> report -> test-edit -> console`를 한 번에 연쇄 실행하는 것
- Unity가 아직 busy인데 `status`, `menu`, `test`를 연속 재투입하는 것

이 구간은 workflow를 바꿔야 한다.

## 저장소 운영 규칙

### 1. 짧은 lane과 긴 lane을 분리한다

짧은 기본 lane:

- `pwsh -File tools/unity-bridge.ps1 status`
- `pwsh -File tools/unity-bridge.ps1 compile`
- `pwsh -File tools/unity-bridge.ps1 prepare-playable`
- `pwsh -File tools/unity-bridge.ps1 quick-battle-smoke`
- `pwsh -File tools/unity-bridge.ps1 content-validate`
- `pwsh -File tools/unity-bridge.ps1 balance-sweep-smoke`
- `pwsh -File tools/unity-bridge.ps1 clear-console`
- `pwsh -File tools/unity-bridge.ps1 console`
- `pwsh -File tools/unity-bridge.ps1 report-town`
- `pwsh -File tools/unity-bridge.ps1 report-battle`
- `pwsh -File tools/unity-bridge.ps1 test-edit -TestFilter <Namespace.Class>`
- `pwsh -File tools/unity-bridge.ps1 test-play -TestFilter <Namespace.Class>`

긴 manual artifact lane:

- `pwsh -File tools/unity-bridge.ps1 loopd-slice`
- `pwsh -File tools/unity-bridge.ps1 loopd-purekit`
- `pwsh -File tools/unity-bridge.ps1 loopd-systemic`
- `pwsh -File tools/unity-bridge.ps1 loopd-runlite`
- `pwsh -File tools/unity-bridge.ps1 loopd-smoke`
- `pwsh -File tools/unity-bridge.ps1 loopd-full`

긴 lane은 CI 기본 test lane이 아니라, shardable manual validation lane으로 취급한다.
`pwsh -File tools/pre-art-rc.ps1`는 blocking automated floor를 순차 실행하지만,
Loop D는 여전히 shard lane으로 개별 phase를 기록한다.

### 2. default `test-edit`는 targeted oracle용이다

- default `test-edit`는 짧은 EditMode oracle을 회수하는 용도다.
- multi-scenario deterministic suite, artifact generation, balance sweep는 여기에 넣지 않는다.
- 장시간 smoke는 `[Explicit]` 또는 equivalent manual lane으로 분리한다.

### 3. `MenuItem`은 짧은 editor 작업에만 쓴다

- seed generation, bootstrap, 짧은 validator처럼 명확히 끝나는 작업만 menu lane에 둔다.
- multi-minute sweep는 custom CLI tool 또는 manual shard lane으로 분리한다.

### 4. transport를 오해하지 않는다

- CLI/MCP가 응답하지 않는다고 바로 툴 고장으로 보지 않는다.
- 먼저 Unity가 Test Runner 또는 long callback 안에 들어간 것인지 확인한다.
- CPU가 계속 움직이고 Editor log가 같은 callback 안에 머물면, transport보다 workload routing 문제로 본다.

## Hold on 상태창이 떴을 때 판단 순서

1. Unity Editor process가 살아 있고 CPU를 계속 쓰는지 본다.
2. `Editor.log`에 `TestJobRunner.ExecuteCallback`, `ExecuteStep`, 장시간 menu callback이 있는지 본다.
3. `pwsh -File tools/unity-bridge.ps1 status`가 즉시 안 돌아와도 곧바로 CLI/MCP 고장으로 단정하지 않는다.
4. 같은 command를 연타하지 말고, 현재 callback이 끝날 때까지 기다린다.
5. 반복되면 해당 workload를 default lane에서 빼고 shard/manual lane으로 이동한다.

## Loop D 기준 운영 규칙

- `LoopDTelemetryAndBalanceTests`의 장시간 smoke는 default EditMode suite에서 제외한다.
- Loop D artifact smoke/full evidence는 `FirstPlayableBalanceRunner` test가 아니라 `loop_d_balance_report`
  custom tool과 `tools/unity-bridge.ps1 loopd-*` verb로 회수한다.
- release floor wrapper도 `loopd-slice -> loopd-purekit -> loopd-systemic -> loopd-runlite`를 그대로 사용한다.
- `SM/Internal/Validation/Run Loop D Balance Smoke`는 기본 경로가 아니다.
- Loop D full suite는 CI mandatory lane이 아니라 manual artifact lane이다.

## recovery runbook

### case 1. 짧은 busy 후 회복

1. 10~30초 기다린다.
2. `pwsh -File tools/unity-bridge.ps1 status`
3. `pwsh -File tools/unity-bridge.ps1 console -Lines 120 -Filter error,warning,log`

### case 2. Test Runner callback 장기 점유

1. 현재 `test-edit` / menu command를 추가로 던지지 않는다.
2. `Editor.log`에서 어떤 test/class/menu가 오래 도는지 먼저 확인한다.
3. 해당 작업을 다음부터는 `-TestFilter` 또는 `loopd-*` shard lane으로 옮긴다.

### case 3. 실제 hang 의심

아래가 동시에 보이면 실제 hang 가능성이 높다.

- Unity process CPU 사용량이 거의 0으로 오래 유지됨
- `Editor.log`가 진행 없이 같은 지점에서 멈춤
- `status` heartbeat가 오랫동안 회복되지 않음
- wait 대상 artifact도 갱신되지 않음

이 경우에만 editor restart를 escalation로 본다.

## 권장 검증 시퀀스

### 일반 코드 수정

1. `pwsh -File tools/unity-bridge.ps1 compile`
2. 필요 시 `pwsh -File tools/unity-bridge.ps1 test-edit -TestFilter <Namespace.Class>`
3. 필요 시 `pwsh -File tools/unity-bridge.ps1 console`

### Loop D telemetry/readability 확인

1. `pwsh -File tools/unity-bridge.ps1 compile`
2. `pwsh -File tools/unity-bridge.ps1 loopd-slice`
3. `pwsh -File tools/unity-bridge.ps1 loopd-purekit`
4. `pwsh -File tools/unity-bridge.ps1 loopd-systemic`
5. `pwsh -File tools/unity-bridge.ps1 loopd-runlite`
6. 필요 시 마지막에만 `pwsh -File tools/unity-bridge.ps1 loopd-smoke`

## 결론

Unity 작업이 느린 부분은 일부 불가피하다.
하지만 지금까지 반복된 hold의 대부분은 `CLI/MCP 자체가 불안정해서`라기보다, 장시간 workload를 잘못된 lane에
태운 결과다.

이 저장소의 기본 정책은 다음으로 고정한다.

- 짧은 compile/report/test는 CLI fast lane
- typed editor mutation은 MCP lane
- 장시간 balance/readability/prune sweep는 shardable manual lane
