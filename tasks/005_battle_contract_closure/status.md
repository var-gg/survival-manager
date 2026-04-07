# 작업 상태

## 메타데이터

- 작업명: Battle Contract Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-07

## Current state

- playback-safe presentation core 1차 적용 완료
- `RefreshAfterSeek()`는 `ClearTransients -> RenderSnapshot -> SetFocus` snapshot-only path로 교체됨
- `Replay` / `Rebattle` / `RestartSameSeed` 의미 분리와 explicit `Replay` 버튼 추가
- normal lane current/selected telegraph, aggregate HUD, tactical selected card, suggested camera framing 1차 반영
- 관련 design / architecture 문서는 새 계약 기준으로 갱신됨

## Acceptance matrix

| 항목 | 요구 | 현재 상태 | 근거 / 다음 확인 |
| --- | --- | --- | --- |
| compile | battle/runtime/profile 변경 후 compile green | 부분 | `unity-bridge compile`는 열린 Unity 인스턴스/port 8090 대기에서 timeout. 대신 Unity embedded Roslyn + Bee rsp 임시 compile로 신규 battle presentation 파일군 compile error 없음 확인. worktree의 unrelated runtime 변경군 오류는 별도 존재 |
| validator | profile/localization drift 검증 | 부분 | 이번 패스의 주 대상은 battle presentation이며, 별도 validator/report loop는 돌리지 않았다 |
| targeted tests | snapshot/cue/presenter/camera 경로 검증 | 부분 | `test-batch-fast` wrapper는 project lock 경고 후 기존 FastUnit 결과만 회수. 새 `BattlePresentationCueBuilderTests`, `BattleCameraFramingPolicyTests`, `BattleScreenPresenterReadabilityTests`, `BattlePresentationSnapshotTests` 추가 |
| runtime smoke | normal lane / debug lane scene smoke | 대기 | Unity project lock과 compile timeout 때문에 observer scene smoke는 이번 turn에 재수행하지 못함 |
| docs policy | docs/task 거버넌스 검증 | 완료 | `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .` 통과 |
| docs lint | markdownlint | 부분 | `pwsh -File tools/docs-check.ps1 -RepoRoot .` 재실행. 현재 작업 문서의 root-level 표 형식 오류 1건 수정 후 재검증했으며, 남은 실패는 저장소 전반의 기존 markdownlint debt(`docs/02_design/meta/augment-catalog-v1.md`, `docs/03_architecture/testing-strategy.md`, `.claude/worktrees/**`, `Packages/com.coplaydev.unity-mcp/README.md` 등)다 |
| smoke check | repo smoke | 완료 | `pwsh -File tools/smoke-check.ps1 -RepoRoot .` 통과 |

## Evidence

- 구현 요약:
  - `BattlePresentationController`를 `RenderSnapshot` / `AdvanceStep` / `SetBlend` / `TickTransients` / `SetFocus` 경로로 분리
  - `BattlePresentationCueBuilder`, `BattlePresentationCue`, `BattleReadabilityFormatter`, `BattleCameraFramingPolicy` 추가
  - `BattleActorView`에 current/selected telegraph, slot/range/tether surface, step-locked transient timer 반영
  - `BattleScreenPresenter`를 aggregate summary + compact log + `Replay` 버튼 + tactical selected card 기준으로 압축
  - `BattleCameraController`에 bootstrap/passive suggested framing과 manual hold 추가
- 검증:
  - `pwsh -File tools/unity-bridge.ps1 test-batch-fast` (project lock 경고 후 기존 FastUnit 결과 회수)
  - `pwsh -File tools/test-harness-lint.ps1`
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - `pwsh -File tools/docs-check.ps1 -RepoRoot .` (root-level 현재 작업 문서 오류 1건 수정 후 재실행, 남은 실패는 repo-wide pre-existing markdownlint debt)
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
  - Unity embedded Roslyn + Bee rsp 임시 compile
    - 신규 battle presentation 파일군 compile error 없음
    - unrelated dirty worktree runtime/test 변경군 오류 다수 존재

## Remaining blockers

- 열린 Unity 인스턴스 때문에 `unity-bridge compile` / `test-batch-fast`가 안정적으로 새 결과를 회수하지 못한다.
- 현재 worktree에는 이 task와 무관한 runtime/test 변경군 compile 오류가 남아 있어,
  full-project green을 이 작업 단독으로 확정할 수 없다.
- normal lane readability는 1차 closure 수준이며, 실제 scene smoke에서 thickness/timing 조정이 더 필요하다.

## Deferred / debug-only

- facing 기반 guard arc 세분화
- primitive cue 타이밍/두께 미세 tuning
- same-seed restart UI 노출 여부 최종 정리

## Loop budget consumed

- compile-fix: 2
- refresh/read-console: 2
- roslyn-backstop: 1
- budget 초과 시 남긴 diagnosis:
  - 열린 editor / project lock이 batch compile 신뢰도를 크게 떨어뜨린다.
  - Bee rsp + embedded Roslyn은 code-only sanity check로는 유효하지만,
    현재 worktree의 unrelated 오류까지 함께 노출된다.

## Handoff notes

- 시작 문서: 이 status -> spec -> plan -> implement
- seek/replay correctness 확인은 `F3 off`, `Pause`, `Seek`, `Replay`, `Rebattle`, `x4`를 한 세트로 본다.
- full compile/test green을 확정하려면 먼저 unrelated dirty worktree 오류를 분리해야 한다.
- scene smoke에서는 current actor ring / target reticle / selected tether / passive camera hold 충돌을 우선 확인한다.
