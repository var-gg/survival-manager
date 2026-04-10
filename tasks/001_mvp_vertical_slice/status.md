# 작업 상태: 001 MVP Vertical Slice

- 상태: 진행 중
- 최종수정일: 2026-04-10
- 단계: prototype
- 작업 ID: 001

## 먼저 실행할 메뉴

- `SM/Play/Full Loop`
- `SM/Play/Combat Sandbox`

## 이 문서의 역할

- playable boundary, release blocker, 다음 우선순위의 live source of truth
- `docs/06_production/**`는 이 상태 문서를 요약하거나 packet 운영 규칙을 보조하는 역할만 가진다.

## 관련 운영 계약

- newcomer / smoke / recovery / RC floor는 `docs/06_production/pre-art-release-floor.md`
- validator / compile / targeted test / runtime smoke 관계는 `docs/03_architecture/validation-and-acceptance-oracles.md`
- combat / Loop D shard lane은 `docs/03_architecture/combat-harness-and-debug-contract.md`

## 현재 검증된 playable 경계

- `Boot -> Town -> Expedition -> Battle -> Reward -> Town`
- Town active surface는 chapter/site 선택, `Start Expedition`, `Resume Expedition`, secondary `Quick Battle (Smoke)`다.
- 사람이 기억하는 top-level entry는 `SM/Play/Full Loop`와 `SM/Play/Combat Sandbox` 두 개다.
- direct Combat Sandbox는 pure battle lane이고, Town `Quick Battle (Smoke)`는 integration smoke lane이다.

## 현재 구현 상태

- fixed-step live simulation, authored chapter/site/encounter, Reward settlement, Town resume semantics는 구현돼 있다.
- extract settlement도 `Reward -> Town(close)`로 끝나는 authored loop를 사용한다.
- save/load, localization foundation, scene repair/bootstrap, sample content bootstrap, observer report surface가 저장소에 들어와 있다.
- narrative runtime core(`SM.Core` enum, `SM.Content` narrative definition, `SM.Meta` story director/progress/save host, `GameSessionState` binding)까지는 코드로 들어와 있다.
- `StorySceneFlowBridge`, `StoryPresentationRunner`, 4종 presenter/view/UXML/USS가 들어와 있어 Town/Expedition/Battle/Reward scene에서 narrative presentation queue를 소비할 수 있다.
- release-floor tooling은 `prepare-playable` canonical lane, `quick-battle-smoke` combat sandbox lane, `repair-scenes` / `ensure-localization` recovery lane으로 정리됐다.
- `SM/Authoring/Combat Sandbox`가 scenario library, active handoff sync, preview, batch run의 주 authoring surface로 승격됐다.
- `tools/pre-art-rc.ps1`와 `docs/06_production/pre-art-release-floor.md`가 same-SHA automated floor와 packet 경로를 남기도록 추가됐다.
- `tools/unity-bridge.ps1 test-batch-fast|test-batch-edit`는 stale `TestResults-Batch.xml`을 더 이상 success evidence로 재사용하지 않는다.

## release-floor snapshot

- 현재 남은 5%는 새 gameplay 시스템 추가가 아니라 same-SHA evidence refresh, lane naming 정리, RC packet 운영화다.
- latest packet attempt:
  - `Logs/release-floor/20260408-020521-41ebbb3/manifest.json`
- current pass:
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
  - `pwsh -File tools/test-harness-lint.ps1 -RepoRoot .`
  - `$paths = @(...); & .\tools\docs-check.ps1 -RepoRoot . -Paths $paths`
- current blocker:
  - `pwsh -File tools/unity-bridge.ps1 compile`가 `Waiting for Unity... timed out waiting for Unity (port 8090)`로 실패한다.
  - batch lane은 열린 Unity 인스턴스 때문에 project lock으로 중단된다.
- dirty worktree note:
  - `Assets/_Game/**`, `docs/02_design/ui/**`, `docs/03_architecture/localization-runtime-and-content-pipeline.md` 등 사용자 변경이 열린 상태에서 로컬 evidence를 수집했다.

## 아직 남은 리스크

- same-SHA compile / PlayMode / Loop D shard / observer report fresh evidence는 아직 final green이 아니다.
- clean clone newcomer witness, normal loop, localization, save/load, recovery manual sign-off가 packet과 `status.md`에 아직 남지 않았다.
- art/presentation polish, content breadth, online seam은 여전히 out-of-scope open item이다.
- narrative seed content, validator, same-SHA Unity compile evidence는 아직 deferred다.

## Evidence

- commit SHA baseline: `41ebbb3d8b2f65ef288cc485cbea4502aa34daae`
- RC packet:
  - command: `pwsh -File tools/pre-art-rc.ps1 -UnityRecoveryBudget 0`
  - artifact: `Logs/release-floor/20260408-020521-41ebbb3/manifest.json`
  - result: compile phase fail
- fresh blocker confirmation:
  - `pwsh -File tools/unity-bridge.ps1 status`
    - result: `unity-cli connector remained busy after 5 attempts. Unity (port 8090): not responding (last heartbeat 105h54m36s ago)`
  - `pwsh -File tools/unity-bridge.ps1 test-batch-fast`
    - result: `Another Unity instance may still hold the project lock.`
  - `pwsh -File tools/unity-bridge.ps1 content-validate`
    - artifact: `Logs/content-validation-ci.log`
    - result: project lock으로 batch executeMethod fail
  - current editor log diagnostic:
    - `Assets/_Game/Scripts/Runtime/Meta/Services/PassiveBoardSelectionValidator.cs:145`
    - `Assets/_Game/Scripts/Runtime/Meta/Services/PermanentAugmentProgressionService.cs:29,50`
    - note: current opened editor session에서 Meta compile error가 보이며, clean same-SHA rerun 전까지 final evidence로 채택하지 않는다.
- lane skipped:
  - `test-play`, `loopd-*`, `prepare-playable`, `report-town`, `report-battle`는 compile ready가 current SHA에서 닫히지 않아 final evidence로 채택하지 않았다.

## 다음 우선순위

1. multiple Unity instances / connector busy를 정리한 뒤 same-SHA `pre-art-rc` green packet을 다시 회수한다.
2. clean clone newcomer witness와 manual normal loop / Quick Battle smoke / localization / save-load / recovery sign-off를 `summary.md`와 status에 남긴다.
3. 남은 open item을 art/presentation/content breadth/online 후속으로만 제한하고 paid asset pass로 넘어간다.
