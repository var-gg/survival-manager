# specialist encounter/tuning/localization 명세

## 메타데이터

- 작업명: specialist encounter/tuning/localization
- 담당: Codex
- 상태: completed
- 최종수정일: 2026-04-19
- 관련경로:
  - `Assets/Resources/_Game/Content/Definitions/**`
  - `Assets/Localization/StringTables/**`
  - `Assets/_Game/Scripts/Editor/SeedData/SampleSeedGenerator.cs`
- 관련문서:
  - `docs/02_design/meta/item-passive-augment-budget.md`
  - `docs/03_architecture/first-playable-balance-targets.md`
  - `tasks/020_canonical_resources_audit_green/status.md`

## Goal

- specialist 4종의 enemy encounter coverage를 canonical 24 encounter 안에 배치하고, content validation 잔여 governance issue를 닫는다.
- 장비/affix/augment/passive 수치는 budget window 안에서 보수적으로 정리한다.
- 한글/영문 localization은 기존 key/table 구조를 유지하고 specialist 관련 문구 톤만 정리한다.

## Authoritative boundary

- authored truth는 committed Resources asset과 localization string table asset이다.
- `SM.Meta` pure boundary는 유지하며, `Assets/_Game/Scripts/Runtime/Meta/**`에 `SM.Content`, `UnityEngine`, `UnityEditor` 참조를 추가하지 않는다.
- encounter 수는 24개로 유지한다.

## In scope

- `rift_stalker`, `bastion_penitent`, `pale_executor`, `mirror_cantor`를 각자 family에 맞는 enemy squad에 배치한다.
- 각 specialist는 enemy squad 전체 기준 최소 2회, elite/boss squad 기준 최소 1회 등장하도록 한다.
- `loop_c.condition_cap`, `loop_c.keyword_cap`, `tag.missing_id`, `loop_b.recruit.banned_pairing`, build lane warning을 수정한다.
- 기존 localization key/table 체계 안에서 specialist join/toast/lore/encounter 문구를 정리한다.

## Out of scope

- 새 localization system/table 추가
- scene/prefab/component 구조 편집
- encounter count 증가
- 자동 튜너 도입
- session service object extraction

## asmdef impact

- production asmdef 변경은 없다.
- `SM.Meta`, `SM.Meta.Serialization`, `SM.Core`, `SM.Combat`, `SM.Persistence.Abstractions` pure boundary를 유지한다.

## persistence impact

- persistence contract 변경은 없다.
- specialist id와 hero id는 기존 authoring truth를 따른다.

## validator / test oracle

- `pwsh -File tools/unity-bridge.ps1 content-validate`
- `pwsh -File tools/unity-bridge.ps1 balance-sweep-smoke`
- 필요 시 `pwsh -File tools/unity-bridge.ps1 loopd-smoke`
- focused tests: `ContentValidationWorkflowTests`, `LoopCContentGovernanceTests`, `EncounterAndLootResolutionTests`, `CharacterAxisLocalizationTests`, `StoryDirectorServiceTests`

## done definition

- `content-validate` error 0.
- specialist 4종은 지정 family의 enemy squad에 등장하고 각자 total 2회 이상, elite/boss 1회 이상을 만족한다.
- budget window와 first playable KPI 문서의 금지 원칙을 위반하지 않는다.
- localization table 구조는 늘리지 않고 기존 key tone만 정리한다.
- scene/prefab 변경이 없다.

## deferred

- docs markdownlint repo-wide cleanup은 022에서 처리한다.
- `GameSessionState` service object 2차 분해는 023에서 처리한다.
