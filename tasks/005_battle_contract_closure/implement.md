# 작업 구현

## 메타데이터

- 작업명: Battle Contract Closure
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01
- 실행범위: battle runtime / content profiles / UI localization / sandbox docs

## Phase log

- Phase 0 preflight
  - battle runtime, presentation, localization, sandbox, validator
    구조를 재검토했다.
  - 현재 `BattleActorView`가 world-space와 overlay를 동시에 만들고,
    `UI_Battle` table이 비어 있다는 사실을 확인했다.
- Phase 1 code-only
  - footprint/behavior/mobility profile 타입, slotting,
    reevaluation, hit resolution, screen-space overhead UI,
    localization fallback을 반영했다.
- Phase 2 asset authoring
  - sample content generator에 profile asset 생성을 추가했다.
  - 첫 시도는 Play Mode/observer 루프와 섞여 archetype authoring이
    부분 저장됐고, Play Mode 밖에서 재실행해 profile asset과
    archetype reference를 복구했다.
- Phase 3 validation
  - `compile`, `bootstrap`, `report-battle`, `console`,
    `docs-policy-check`, `smoke-check`를 실행했다.
  - `test-edit`는 `unity-cli` 응답이 끊기며 editor가 일시적으로
    멈춰서 결과를 회수하지 못했다.

## deviation

- runtime smoke는 green이지만, EditMode test runner 결과 회수는
  아직 미완료다.

## blockers

- `pwsh -File tools/unity-bridge.ps1 test-edit`가
  `run_tests sent (connection closed before response)` 이후 editor를
  일시적으로 hang시켜 결과를 안정적으로 회수하지 못한다.

## diagnostics

- `pwsh -File tools/unity-bridge.ps1 compile`
  - green
- `pwsh -File tools/unity-bridge.ps1 bootstrap`
  - menu dispatch 성공
- `pwsh -File tools/unity-bridge.ps1 report-battle`
  - battle scene contract object / controller / overlay root 확인
- `pwsh -File tools/unity-bridge.ps1 console -Lines 160 -Filter error,warning,log`
  - smoke 직후 console empty
- direct `unity-cli menu 'SM/Seed/Generate Sample Content'`
  - profile asset 생성 후 launch-floor archetype reference를 복구했다.
- `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - passed
- `pwsh -File tools/smoke-check.ps1 -RepoRoot .`
  - passed
- `pwsh -File tools/docs-check.ps1 -RepoRoot .`
  - repo-wide markdownlint debt 710건으로 red.
  - 이번 task 신규 문서도 일부 line-length debt가 있었고 정리했지만,
    기존 전체 debt가 압도적이다.

## why this loop happened

- battle observer path가 presentation, localization bootstrap,
  content authoring 경계를 동시에 건드리는 구조라서 compile/asset
  loop가 섞이기 쉽다.
- 이번 task는 code-only와 asset authoring을 분리해서 같은 루프가
  다시 생기지 않게 한다.
