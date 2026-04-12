# Pre-Art Release Floor

- 상태: active
- 소유자: repository
- 최종수정일: 2026-04-09
- 소스오브트루스: `docs/06_production/pre-art-release-floor.md`
- 관련문서:
  - `README.md`
  - `docs/TESTING.md`
  - `docs/05_setup/local-runbook.md`
  - `docs/05_setup/quick-battle-smoke.md`
  - `docs/05_setup/scene-repair-bootstrap.md`
  - `docs/03_architecture/validation-and-acceptance-oracles.md`
  - `docs/03_architecture/combat-harness-and-debug-contract.md`
  - `tasks/001_mvp_vertical_slice/status.md`
  - `tasks/012_loop_d_telemetry_pruning_readability_balance_closure/status.md`
  - `tasks/015_session_realm_authority_boundary/status.md`

## 목적

paid asset pass 직전, 이 저장소에서 시스템적 blocker가 닫혔는지 같은 SHA 기준으로 확인하는 운영 floor를 정의한다.
이 문서는 art/presentation polish 계획이 아니라 playable floor와 release evidence lane을 고정한다.

## canonical / smoke / recovery 구분

- canonical newcomer lane:
  - `SM/전체테스트`
  - `Boot -> Start Local Run -> Town -> Expedition -> Battle -> Reward -> Town`
- smoke lane:
  - Town secondary `Quick Battle (Smoke)`
  - `SM/전투테스트`
  - `pwsh -File tools/unity-bridge.ps1 quick-battle-smoke`
- recovery lane:
  - `SM/Internal/Recovery/Repair First Playable Scenes`
  - `SM/Internal/Recovery/Ensure Localization Foundation`
  - `SM/Internal/Content/Ensure Sample Content`
  - runtime rebind / direct-scene fallback

smoke와 recovery는 canonical acceptance를 대체하지 않는다.

## blocking automated floors

automated floor는 같은 SHA에서 아래 순서로 green이어야 한다.

1. `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
2. `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
3. `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
4. `pwsh -File tools/unity-bridge.ps1 compile`
5. `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
6. `pwsh -File tools/unity-bridge.ps1 content-validate`
7. `pwsh -File tools/unity-bridge.ps1 balance-sweep-smoke`
8. `pwsh -File tools/unity-bridge.ps1 test-batch-edit`
9. `pwsh -File tools/unity-bridge.ps1 test-play`
10. `pwsh -File tools/unity-bridge.ps1 loopd-slice`
11. `pwsh -File tools/unity-bridge.ps1 loopd-purekit`
12. `pwsh -File tools/unity-bridge.ps1 loopd-systemic`
13. `pwsh -File tools/unity-bridge.ps1 loopd-runlite`
14. `pwsh -File tools/unity-bridge.ps1 prepare-playable`
15. `pwsh -File tools/unity-bridge.ps1 report-town`
16. `pwsh -File tools/unity-bridge.ps1 report-battle`

`tools/smoke-check.ps1`는 runtime smoke가 아니라 repo structure preflight다.
runtime smoke는 `test-play`, manual newcomer witness, manual normal loop가 맡는다.
batchmode lane은 stale `TestResults-Batch.xml`이나 stale validator log를 success evidence로 재사용하지 않는다.

## RC wrapper

자동 floor 기본 진입점은 아래 wrapper다.

```powershell
pwsh -File tools/pre-art-rc.ps1
```

wrapper 원칙:

- monolithic one-shot Unity callback으로 묶지 않는다.
- docs preflight, compile, batch lane, play lane, Loop D shard, observer report를 분리해서 순차 실행한다.
- 같은 SHA 기준 packet을 `Logs/release-floor/<timestamp>-<shortsha>/` 아래에 남긴다.
- Unity busy가 보이면 `focus-unity.ps1`, `wait-unity-ready.ps1`, `status` polling을 recovery budget 안에서만 사용한다.

## packet

RC wrapper는 최소 아래 파일을 남긴다.

- `Logs/release-floor/<timestamp>-<shortsha>/manifest.json`
- `Logs/release-floor/<timestamp>-<shortsha>/summary.md`
- `Logs/release-floor/<timestamp>-<shortsha>/town_observer_contract.json`
- `Logs/release-floor/<timestamp>-<shortsha>/battle_observer_contract.json`

같은 SHA에서 참조해야 하는 machine artifact:

- `Logs/content-validation/**`
- `Logs/content-validation-ci.log`
- `Logs/balance-sweep/**`
- `Logs/balance-sweep-ci.log`
- `Logs/loop-d-balance/first_playable_slice.md`
- `Logs/loop-d-balance/purekit_report.json`
- `Logs/loop-d-balance/systemic_slice_report.json`
- `Logs/loop-d-balance/runlite_report.json`
- `Logs/loop-d-balance/content_health_cards.csv`
- `Logs/loop-d-balance/prune_ledger_v1.json`
- `Logs/loop-d-balance/readability_watchlist.json`
- `Logs/loop-d-balance/loop_d_closure_note.txt`

generated artifact는 `Logs/*`와 CI artifact로 보관하고, durable sign-off는 문서와 status에만 남긴다.

## manual sign-off

아래 항목은 자동화로 대체하지 않는다.

- clean clone newcomer witness: Unity `6000.4.0f1`만으로 `SM/전체테스트 -> Boot -> Start Local Run`
- normal loop smoke: first reward return, selector lock, `Resume Expedition`, boss extract `Reward -> Town(close)`
- Quick Battle smoke: campaign progression 비오염 확인
- localization: `ko` / `en` overlay 전환, `ui.battle.*` missing key 0
- save/load: Town UI round-trip
- recovery: `SM/Internal/Recovery/Repair First Playable Scenes`, `SM/Internal/Recovery/Ensure Localization Foundation`, `SM/Internal/Content/Ensure Sample Content`

manual 결과는 PR 댓글이나 개인 메모에만 남기지 않는다.
최소 `summary.md`와 관련 `status.md`에 같이 남긴다.

## durable sign-off source of truth

- playable boundary와 release blocker 정리: `tasks/001_mvp_vertical_slice/status.md`
- Loop D telemetry/readability/prune evidence: `tasks/012_loop_d_telemetry_pruning_readability_balance_closure/status.md`
- session seam / authority / recovery evidence: `tasks/015_session_realm_authority_boundary/status.md`

모든 `status.md` evidence 섹션은 최소 아래를 포함한다.

- 실행한 명령
- artifact 경로
- commit SHA
- unresolved blocker 여부
- lane 생략 이유

## paid asset pass 선언 기준

아래가 동시에 참이면 pre-art complete로 본다.

- newcomer가 README와 runbook만으로 canonical playable을 재현할 수 있다.
- same-SHA automated floors가 green이다.
- normal loop, Quick Battle smoke, localization, save/load, recovery manual sign-off가 남아 있다.
- `tasks/001`, `tasks/012`, `tasks/015` evidence가 same-SHA 기준으로 refresh됐다.
- open item이 art/presentation/content breadth/online seam 같은 out-of-scope뿐이다.
