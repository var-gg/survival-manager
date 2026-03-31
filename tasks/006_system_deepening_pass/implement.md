# 작업 구현

## 메타데이터

- 작업명: System Deepening Pass
- 담당: Codex
- 상태: 진행 중
- 최종수정일: 2026-04-01
- 실행범위: schema scaffold, validator/report, design/architecture docs, Markdown catalog

## Phase log

- Phase 0 preflight
  - `tasks/005_battle_contract_closure/status.md`와 current docs index를 다시 읽어 battle baseline과 문서 체계를 확인했다.
  - current source-of-truth는 `docs/02_design/**`, `docs/03_architecture/**`이며 새 `docs/contracts` 루트는 만들지 않기로 고정했다.
- Phase 1 code-only
  - `AffixDefinition`, `SkillDefinitionAsset`, `AugmentDefinition`, `StatusFamilyDefinition`, `StatusApplicationRule`, `UnitArchetypeDefinition`에 additive schema 필드를 추가한다.
  - `ContentDefinitionValidator`와 `BalanceSweepRunner`를 새 schema/metric scaffold 기준으로 확장한다.
  - `ContentAuthoringSchemaTypes.cs`를 추가해 affix family, skill template/AI, augment offer bucket, status stack/ownership enum을 한 파일에 모았다.
  - `ContentValidationWorkflowTests`에 deep schema drift test와 balance sweep CSV column test를 추가했다.
- Phase 2 asset authoring
  - 대량 asset 재생성 대신 Markdown catalog를 source-of-truth로 추가하고, current asset은 default/fallback로 유지한다.
  - 다만 Unity compile/test loop 중 `Assets/Resources/_Game/Content/Definitions/**`와 localization settings가 새 serialized field를 반영하며 광범위하게 reserialize되었다.
- Phase 3 validation
  - `compile`, `docs-policy-check`, `smoke-check`는 성공했다.
  - `docs-check`는 repo-wide markdownlint debt와 이번 task line-length debt 때문에 red였다.
  - `test-edit`는 `run_tests sent (connection closed before response)` 이후 결과 회수를 못 했고, 뒤이은 `report-battle` / `console`도 Unity port timeout으로 이어졌다.

## deviation

- 계획상 PR 1과 PR 2를 분리했지만, 이번 세션에서는 두 phase의 문서와 scaffold를 한 번에 넣었다.
- full runtime live subset wiring은 하지 않았고, Markdown catalog + schema field + validator scaffold까지로 제한했다.

## blockers

- `unity-cli test` 응답 채널이 여전히 불안정하다.
- 새 serialized field가 기존 content asset에 광범위한 reserialize를 일으켜 diff footprint가 커졌다.

## diagnostics

- current repo는 이미 battle presentation / spatial / behavior / glossary 문서를 active source로 갖고 있다.
- existing synergy validator는 `2 / 3 / 4`를 강하게 전제한다.
- current battle compile contract는 여전히 `core_active / utility_active / passive / support` 4-slot이다.
- docs policy 오탐은 `- status:` / `- owner:` 같은 bullet 문구였고, backtick으로 감싸 해결했다.
- `BalanceSweepRunner`는 새 metric field를 JSON/CSV artifact에 싣되, 계산은 existing battle step/event stream 기반으로 제한했다.

## why this loop happened

- 이전 pass에서 battle readability와 spacing을 닫은 뒤, content schema와 rulebook이 얕게 남아 다음 작업자가 구현을 시작하기 전에 기준을 다시 풀어야 하는 상태가 됐다.
- 이번 pass는 그 drift를 막기 위해 code와 docs를 같은 task 안에서 묶는다.
- 동시에 Unity가 새 serialized field를 기존 asset에 주입하면서, 의도한 code/doc 범위보다 worktree footprint가 커졌다.
