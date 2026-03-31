# Unity MCP tooling contract

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/unity-mcp-tooling-contract.md`
- 관련문서:
  - `docs/03_architecture/unity-agent-harness-contract.md`
  - `docs/03_architecture/unity-editor-iteration-and-asset-authoring.md`
  - `docs/05_setup/unity-mcp.md`
  - `docs/05_setup/unity-cli.md`

## 목적

이 문서는 Unity editor 상태를 low-level tool 조합으로 직접 운전하지 않고, high-level capability 단위로 다루기 위한 계약을 정의한다.
여기서 말하는 capability는 실제 구현이 CLI든 MCP든 custom tool이든 상관없이 동일한 의미를 가져야 한다.

## capability 계약

| capability | 목적 | 필수 preflight | 기대 출력 | 실패 시 동작 |
| --- | --- | --- | --- | --- |
| `ensure_edit_mode` | Edit Mode 보장, Play Mode 금지 작업 차단 | 현재 editor 상태 조회 가능 | editor state, play mode 여부, 차단 이유 | asset/menu 작업 중단 |
| `run_targeted_compile` | 관련 asmdef compile만 확인 | 대상 asmdef와 scope 정의 | compile 성공/실패 요약, error digest | 같은 sprint에서 2회 초과 시 diagnosis 전환 |
| `capture_unity_error_digest` | console flood 대신 blocker 요약 | 최근 실행 command 문맥 | 핵심 error/warning 묶음, 관련 경로 | status에 blocker 기록 |
| `batch_generate_content_assets` | generator/menu 기반 authoring을 batch로 실행 | Edit Mode, compile green, batch scope 정의 | 생성/수정 asset 목록, partial failure 여부 | blind retry 대신 authoring transaction 진단 |
| `validate_content_contracts` | validator-first contract 실행 | validator schema 준비 완료 | validator summary, report 경로, fail list | compile loop로 되돌아가지 않고 drift 수정 계획 생성 |
| `run_targeted_editmode_tests` | 관련 경계 EditMode 검증 | test target 정의 | 통과/실패 요약, 실패 test 목록 | child phase blocker로 기록 |
| `run_targeted_runtime_smoke` | 실제 사용자 경로 smoke | validator/test 선행 완료 또는 명시적 예외 | scene/path 결과와 핵심 증거 | smoke 실패를 feature blocker로 유지 |

## capability 사용 순서

1. `ensure_edit_mode`
2. `run_targeted_compile`
3. `batch_generate_content_assets`
4. `validate_content_contracts`
5. `run_targeted_editmode_tests`
6. `run_targeted_runtime_smoke`
7. 필요 시 `capture_unity_error_digest`

## 금지 anti-pattern

- `Read Console -> Refresh Unity -> Read Console`를 습관적으로 반복하는 것
- 상태 확인 없이 `Execute Menu Item`을 바로 호출하는 것
- asset generation과 compile fix를 같은 micro-loop에서 수행하는 것
- generic file edit + generic console read만으로 Editor state machine을 직접 추적하는 것

## 운영 메모

- capability 이름이 있으면 그 수준으로 묶어 보고하고, low-level 호출 목록을 handoff 중심 증거처럼 쓰지 않는다.
- capability가 아직 구현되지 않았다면, task 문서에 필요한 capability를 먼저 적고 부족한 tooling을 blocker로 남긴다.
