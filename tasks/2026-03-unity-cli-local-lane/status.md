# 작업 상태

## 메타데이터
- 작업명: unity-cli local lane pilot
- 담당: Codex
- 상태: in_progress
- 최종수정일: 2026-04-01

## 현재 상태
- `unity-cli` local fast lane pilot 1차 구현과 검증을 완료했다.
- 현재 project Unity version은 `6000.4.0f1`이다.
- 현재 `Packages/manifest.json`과 `Packages/packages-lock.json`에는 `com.coplaydev.unity-mcp`와 `com.youngwoocho02.unity-cli-connector`가 함께 존재한다.
- wrapper, custom report tool, setup/policy/prompt 문서가 같은 변경 단위에서 정리됐다.
- wrapper-first 운용을 강화했고, local install path fallback과 readiness retry를 넣어 transient heartbeat 지연에 더 강하게 만들었다.
- 후속 운영 보강으로 `docs/05_setup/unity-cli.md`와 `tools/README.md`에
  재부팅 후 재연결, compile/test 직후 busy state, split fallback sequence를 추가했다.
- 후속 안정화로 `tools/unity-bridge.ps1`가 `compile`, `bootstrap`, `test-edit`, `test-play` 뒤
  자동 `status` polling을 수행하도록 보강했고, async dispatch 뒤 connector recovery를 wrapper가 먼저 흡수하도록 정리했다.
- observer/report lane과 test lane을 같은 직렬 파이프라인으로 강제하지 않도록 setup/prompt 문서를 분리했다.
- 후속 안정화로 `test-*`가 `run_tests sent (connection closed before response)`로 끝나도
  wrapper가 fresh `TestResults.xml`을 읽어 pass/fail을 재판정하도록 보강했다.
- 후속 안정화로 `seed-content` explicit preflight verb를 추가했고,
  runtime/test 경로가 canonical sample content를 암묵 regenerate하지 않도록 운영 기준을 분리했다.

## 완료
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
- 후속 안정화로 wrapper가 `connection closed before response` 이후 busy state를 더 오래 기다리도록 조정했고,
  prompt/setup 문서도 ready recovery가 내장된 운영 기준으로 갱신했다.

## 보류
- Unity Preferences `Edit -> Preferences -> General -> Interaction Mode -> No Throttling` 수동 적용
- wrapper verb에 `missing_reference_scan`, `screenshot`, `reserialize`를 추가할지 여부는 후속 운영 판단으로 남겨 둠

## 이슈
- 사용자 지시의 ADR 경로는 `adr-0005-editor-bridge-policy.md`였지만 실제 저장소 기준 문서는 `adr-0008-editor-bridge-policy.md`다.
- 현재 워크트리에 unrelated 변경이 많아, 기존 사용자 변경을 덮지 않도록 최소 범위 편집이 필요하다.
- `rg.exe` 실행 권한 문제가 있어 탐색은 PowerShell 기본 명령으로 우회 중이다.
- 이 환경에서 `unity-cli --project .` 또는 백슬래시 경로는 instance 매칭에 실패했고, 절대 경로를 `A:/...` 형태로 정규화해야 했다.
- bare `unity-cli` command는 설치 직후 또는 기존 셸에서 `PATH`가 바로 반영되지 않을 수 있다.
- `smoke-observer` 실행 뒤 `Unsolicited response received on idle HTTP channel` 메시지가 간헐적으로 출력되지만 검증 결과 자체는 정상 반환됐다.
- `test-edit`와 `test-play`를 병렬 실행하면 Unity 상태가 엉키므로 순차 실행이 필요하다.
- compile 직후나 menu/test dispatch 직후에는 `connection closed before response`,
  `not responding`, `connection refused`가 일시적으로 나올 수 있으므로
  `status -> report/console` 분리 회수 절차를 문서화했다.
- wrapper는 `test-*` 결과 artifact 회수까지 시도하지만, artifact가 갱신되지 않는 경우에는 여전히 불명확 상태가 남을 수 있다.
- `StableTagDefinition` stale asset repair처럼 editor import가 실제로 오래 걸리는 경우에는 wrapper 대기 시간이 길어질 수 있다.
- EditMode test는 `unity-cli` help상 연결을 유지하고 직접 결과를 돌려주는 경로가 원칙이지만,
  실제로 `run_tests sent (connection closed before response)`가 나오면 editor busy/reload 또는 connector 불안정으로 해석해야 한다.
- sample content drift를 test/runtime 경로에서 암묵 repair하면 connector restart와 timeout이 커지므로,
  regenerate는 `seed-content` 같은 explicit preflight lane에서만 수행해야 한다.

## 결정
- 현재 사용 중인 MCP는 유지하며 제거 작업은 하지 않는다.
- `unity-cli`는 gameplay 구현 블록 안이 아니라 별도 infra/pilot task로 다룬다.
- `unity-cli`는 local optional fast lane으로만 문서화한다.
- repo 기본 운영 형태는 `file-first + CLI-fast-lane + MCP-typed-lane`으로 기록한다.
- project-scoped wrapper는 항상 정규화된 절대 경로로 `--project`를 넘긴다.
- custom CLI tool은 raw object 대신 `SuccessResponse` 형태로 반환한다.
- wrapper는 async dispatch 자체보다 connector recovery를 먼저 정상화한 뒤 반환하는 쪽을 기본 정책으로 삼는다.
- test wrapper는 direct result JSON이 없으면 artifact fallback을 시도하고, 실패 테스트는 non-zero로 돌려준다.
- canonical sample content regenerate는 wrapper의 `seed-content` verb와 Unity menu preflight에서만 허용한다.

## 다음 단계
- local 개발 환경에서 `No Throttling` 설정을 적용한다.
- 실제 구현 블록 프롬프트에 `prompts/unity-cli-hybrid-ops.md` 스니펫을 재사용한다.
- 필요 시 `missing_reference_scan`과 screenshot/reserialize를 wrapper verb로 승격한다.
- 필요 시 test artifact 경로를 wrapper가 직접 회수하거나 요약하는 후속 verb를 검토한다.
- sample content implicit regeneration 제거 이후에도 red EditMode suite는 별도 task로 수리한다.
