# 작업 구현

## 메타데이터

- 작업명: Launch Floor Catalog Closure
- 담당: Codex
- 상태: retrofitted
- 최종수정일: 2026-03-31
- 실행범위:
  - `tasks/004_launch_floor_catalog_closure/**`
  - `Assets/_Game/Scripts/Editor/**`
  - `Assets/_Game/Scripts/Runtime/**`
  - `Assets/Resources/_Game/Content/Definitions/**`
  - `Assets/Localization/StringTables/**`

## Phase log

- 이 문서는 분 단위 실행 로그가 아니라, 업로드된 004 로그 요약과 현재
  worktree 흔적을 기준으로 재구성한 phase 요약이다.
- Phase 0 preflight:
  - 실제 실행 초기에 preflight가 약했다.
  - asmdef impact, persistence ownership, validator-first가 시작 전에
    충분히 고정되지 않았다.
- Phase 01 encounter/status/drops:
  - 현재 worktree에는 `Encounters`, `DropTables`, `LootBundles`,
    `RewardSources`, `StatusFamilies` asset 가족이 새로 보인다.
  - runtime 쪽에는 `EncounterResolutionService`, `LootResolutionService`,
    `StatusResolutionService`, `AppliedStatusState`가 새로 보인다.
  - `GameSessionState`, `BattleScreenController`,
    `RuntimeCombatContentLookup` 수정 흔적으로 보아 authored
    encounter/status path를 runtime entry에 연결하려 한 것으로 해석된다.
  - validator와 sample content generator 수정은 이 축의 시작보다 뒤에
    합류한 흔적이 크다.
- Phase 02 skill/support tags:
  - support skill asset, stable tag asset, `RequiredClassTags` 관련 수정
    흔적이 크다.
  - `SkillDefinitionAsset`, `SkillDefinition`, `LoadoutCompiler`,
    `RuntimeCombatContentLookup`, `ContentDefinitionValidator`,
    `SampleSeedGenerator`가 함께 움직였다.
  - support/tag contract closure와 loadout compile closure가 한 축으로
    섞였고, crafting contract까지 같은 문맥으로 묶일 위험이 있었다.
- Phase 03 async arena scaffold:
  - `ArenaSimulationService`, `ArenaModels`, `CampaignProgressModels`,
    arena persistence record 계열 파일이 새로 보인다.
  - 이 축은 async scaffold와 persistence ownership 경계가 같이 열렸다.
  - feature scaffold와 저장 계약이 같이 움직이면서 parent 004가
    과대범위 umbrella가 되었다.

## deviation

- 004는 원래 child phase 3개로 나뉘어야 했지만 parent umbrella 하나로
  먼저 움직였다.
- validator-first 대신 구현 후 validator 확장으로 흐른 지점이 있었다.
- code-only loop와 asset authoring loop가 중간에 섞였다.
- arena scaffold는 feature scaffold보다 persistence ownership 조정이 먼저
  드러났다.

## blockers

- child phase별 compile / validator / runtime smoke evidence가 아직 분리
  기록되지 않았다.
- phase 03은 asmdef/persistence preflight가 다시 필요하다.
- phase 02는 support/tag closure와 crafting contract를 같은 sprint에 둘지
  부터 다시 결정해야 한다.

## diagnostics

- 현재 worktree는 content asset, localization, runtime, editor,
  persistence 경계를 동시에 건드린다.
- 사용자 제공 004 로그 요약에는 다음 증상이 명시돼 있다.
  - asmdef/service 책임 재절단
  - `SampleSeedGenerator`의 Play Mode / reserialize 예외 대응
  - validator의 후행 확장
  - `ArenaSimulationService` persistence 결합 제거
- 이는 단일 결함보다 oversized umbrella task의 전형적 징후에 가깝다.

## why this loop happened

- authoritative migration axis가 하나로 고정되지 않았다.
- acceptance oracle이 늦게 등장해 compile green과 local blocker 해소가
  사실상 목표가 되었다.
- code loop와 asset authoring loop가 Unity의 비싼 refresh / reload 비용
  위에서 섞였다.
- asmdef boundary와 persistence ownership 점검이 preflight가 아니라
  mid-flight에 드러났다.

## 기록 규칙

- 이 문서는 앞으로 micro compile 로그를 누적하는 곳이 아니다.
- 다음 세션부터는 child phase 문서마다 phase별 요약과 oracle evidence만
  남긴다.
