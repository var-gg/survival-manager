# 작업 명세

## 메타데이터

- 작업명: System Deepening Pass
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01
- 관련경로:
  - `Assets/_Game/Scripts/Runtime/Content/Definitions/**`
  - `Assets/_Game/Scripts/Editor/Validation/**`
  - `Assets/Tests/EditMode/**`
  - `docs/02_design/combat/**`
  - `docs/02_design/meta/**`
  - `docs/03_architecture/**`
  - `tasks/006_system_deepening_pass/**`
- 관련문서:
  - `docs/02_design/combat/battle-presentation-contract.md`
  - `docs/02_design/combat/combat-mechanics-glossary.md`
  - `docs/02_design/meta/item-and-affix-system.md`
  - `docs/02_design/meta/synergy-family-catalog.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/03_architecture/sim-sweep-and-balance-kpis.md`

## Goal

- 전투 contract closure 이후 얕게만 정의된 콘텐츠 schema, status/proc rulebook, 경제/offer 정책, balance metric을 다음 구현자가 바로 확장 가능한 기준 문서와 additive scaffold로 끌어올린다.

## Authoritative boundary

- 이번 task는 `콘텐츠 authoring schema + rulebook + catalog source-of-truth` 축만 닫는다.
- battle runtime의 4-slot compile contract와 `2 / 3 / 4` synergy breakpoint는 유지한 채, meta/recruitment와 content authoring 쪽 계약을 깊게 만든다.
- 전투 감상성, spacing, slotting, UI anchor 같은 battle contract는 `tasks/005_battle_contract_closure/` 문서를 reopen하지 않고 참조만 한다.

## In scope

- `AffixDefinition`, `SkillDefinitionAsset`, `AugmentDefinition`, `StatusFamilyDefinition`, `StatusApplicationRule`, `UnitArchetypeDefinition`의 additive schema 확장
- `ContentDefinitionValidator`, `BalanceSweepRunner`, `BalanceSweepReport`의 schema/metric scaffold 확장
- status / keyword / proc rulebook 문서 추가
- skill / affix / synergy / augment / acquisition / reward protection design 문서 추가·개정
- PR 2용 Markdown catalog 문서 추가
- 관련 index와 task handoff 문서 동기화

## Out of scope

- battle compile을 5-slot 이상으로 바꾸는 작업
- 기존 synergy breakpoint를 `2 / 4`로 단순화하는 작업
- full runtime content rollout
- unique/legendary 전면 도입
- meta progression, summon ownership, encounter scripting 전체 closure

## asmdef impact

- 영향 asmdef:
  - `SM.Content`
  - `SM.Editor`
  - `SM.Tests`
- 새 asmdef 추가는 없다.
- `SM.Combat`는 balance metric 산출을 위한 read-only 소비만 유지한다.
- `ScriptableObject` authoring 타입이 `SM.Combat`로 새지 않게 하고, validator/report 책임은 `SM.Editor`에 머문다.

## persistence impact

- save schema 직접 변경은 없다.
- `UnitArchetypeDefinition`의 신규 필드는 meta/recruitment layer authoring 메타데이터이며, current save restore path는 기존 `Skills` compile contract를 계속 사용한다.
- `SM.Meta`, `SM.Unity`, `SM.Persistence.Abstractions` 책임 이동은 없다.

## validator / test oracle

- validator:
  - affix schema consistency
  - skill schema completeness
  - augment offer metadata completeness
  - status stack / refresh / attribution completeness
  - archetype acquisition contract consistency
- targeted EditMode test:
  - `ContentValidationWorkflowTests`
  - 신규 schema/balance metric test
- runtime path smoke:
  - `pwsh -File tools/unity-bridge.ps1 compile`
  - `pwsh -File tools/docs-policy-check.ps1 -RepoRoot .`
  - `pwsh -File tools/docs-check.ps1 -RepoRoot .`
  - `pwsh -File tools/smoke-check.ps1 -RepoRoot .`

## done definition

- task 문서와 phase 문서가 생성되고 `status.md`가 handoff 기준으로 채워져 있다.
- PR 1 범위의 schema/validator/balance report scaffold가 코드에 반영돼 있다.
- design / architecture 문서와 index가 새 source-of-truth를 가리킨다.
- PR 2 범위의 Markdown catalog가 추가되고 live subset / catalog capacity 정책이 문서로 고정돼 있다.
- compile, docs policy, smoke evidence가 `status.md`에 기록돼 있다.

## deferred

- summon ownership rule의 runtime resolver 반영
- target priority / formation / overtime 룰의 full implementation
- catalog 전체를 committed asset으로 연결하는 작업
- public stat layer 확장과 unique/legendary rollout
