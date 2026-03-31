# Phase 01: schema / rulebook

- 상태: in_progress
- 최종수정일: 2026-04-01
- parent task: `tasks/006_system_deepening_pass/status.md`

## authoritative axis

- content schema, status/proc rulebook, acquisition policy, offer protection, balance metric 정의를 source-of-truth 문서와 additive code scaffold로 닫는다.

## in scope

- `AffixDefinition`
- `SkillDefinitionAsset`
- `AugmentDefinition`
- `StatusFamilyDefinition`
- `StatusApplicationRule`
- `UnitArchetypeDefinition`
- `ContentDefinitionValidator`
- `BalanceSweepRunner`
- design/architecture schema 문서

## out of scope

- full catalog authoring
- committed asset 대량 재배치
- full runtime content rollout

## preflight

- `2 / 3 / 4` synergy validator 유지 여부를 먼저 확인한다.
- battle compile이 여전히 4-slot인지 확인한다.
- 새로운 schema가 `SM.Combat`로 새지 않게 `SM.Content` / `SM.Editor`에 머무르게 한다.

## code-only closure

- 신규 필드는 default/fallback을 가져 기존 asset이 곧바로 붕괴하지 않게 한다.
- validator는 presence보다 consistency 우선으로 본다.
- balance report는 새 metric field를 artifact에 포함하고, 계산이 가능한 범위부터 채운다.

## validator / test oracle

- affix schema consistency
- skill schema completeness
- augment offer metadata completeness
- status stack / refresh / attribution completeness
- archetype acquisition contract consistency

## done signal

- compile green
- docs/source-of-truth sync 완료
- schema drift test와 report shape test 추가
- phase 내용이 `status.md` evidence로 연결됨
