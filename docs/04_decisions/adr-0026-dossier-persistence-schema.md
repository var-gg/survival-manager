# ADR-0026 Dossier persistence schema (ludonarrative loop P1a)

- 상태: active
- 소유자: repository
- 최종수정일: 2026-06-03
- 소스오브트루스: `docs/04_decisions/adr-0026-dossier-persistence-schema.md`
- 관련문서:
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/03_architecture/dependency-direction.md`
  - `docs/04_decisions/adr-0023-meta-content-adapter-boundary.md`
  - pindoc `analysis-ludonarrative-loop-implementation` (루프 전체 설계 + 코드 grounding)
  - pindoc `analysis-narrative-engine-retrofit-jrpg` (상위 why: GPT Pro 검수 "전투/고용이 정치극의 원인")

## 문맥

ludonarrative 루프 — 전투/고용이 "정치극 사이 콘텐츠"가 아니라 "정치극의 원인"이 되게 — 의 "전투 → 기록" 절반(P1a)을 구현한다. GPT Pro 검수 핵심: "한새의 dossier가 단순 컷신 소품이면 안 된다. 매 임무 후 dossier는 실제 상태값을 가져야 한다." 따라서 전투 결과를 save truth의 실제 영속 상태로 남길 ledger record가 필요하다.

기존 코드에 `RunSummaryRecord`, `RewardLedgerEntryRecord`, `InventoryLedgerEntryRecord` 같은 ledger 패턴이 이미 있고, 전투 결과(`BattleResult.FinalUnits`)는 `GameSessionState.SessionRewardSettlementFlow.MarkBattleResolved`에 이미 전달된다(`ApplyHeroBattleAftermath`가 같은 데이터를 읽는다). 즉 새 데이터 수집 경로가 필요 없다 — 기존 hook에서 집계만 하면 된다.

## 결정

전투 1회(sortie node) 결과를 캠페인 영구 기록으로 남기는 `DossierEntryRecord`를 추가한다.

구체적으로:

- `DossierEntryRecord`는 `SM.Persistence.Abstractions.Models`가 소유한다 (ADR-0023 "persistence contract ownership은 `SM.Persistence.Abstractions.Models`에 둔다" 정합). 필드: EntryId, RunId, ChapterId, SiteId, NodeId, Result, Outcome, SurvivorAllyCount, TotalAllyCount, FallenAllyIds, CompletedAtUtc.
- `SaveProfile`에 `List<DossierEntryRecord> Dossier` 필드를 추가한다. 다른 ledger 리스트와 동렬, 기본값 `new()` 으로 backward-compatible(구 세이브는 빈 리스트로 로드).
- outcome **분류 판정**은 `SM.Meta.DossierOutcomeClassifier`(순수 static, engine 무참조)가 소유한다 — EditMode 단위 검증 가능. P1a는 squad 생존만 본다(`Defeat`/`CostlyVictory`/`CleanVictory`).
- **집계 + 영속화**는 `SM.Unity`의 `SessionRewardSettlementFlow.WriteDossierEntry`가 수행한다. `finalUnits`에서 ally roster 생존을 세고 classifier로 분류 후 `SaveProfile.Dossier`에 append. sandbox/quick-battle smoke lane은 제외(캠페인 기록 아님).
- `SM.Combat`은 dossier/objective를 알지 않는다. `BattleResult`(전투 사실)만 산출한다 — combat 순수성 보존.

## 검토한 대안

| option | description | pros | cons | verdict |
| --- | --- | --- | --- | --- |
| `option_a_meta_owns_record` | `DossierEntryRecord`를 `SM.Meta`가 소유 | NarrativeProgressRecord와 한곳 | persistence contract는 Persistence.Abstractions 소유(ADR-0023) — 재퇴행 | reject |
| `option_b_combat_emits_outcome` | `SM.Combat`이 objective outcome을 산출 | hook 1곳 | combat 순수성 위반 — combat이 narrative/meta 개념을 앎 | reject |
| `option_c_direct_flag_write` | settlement flow가 story flag를 직접 써서 즉시 정치 분기 | record 없이 루프 1턴에 닫힘 | `StoryDirectorService`가 flag 단일 writer — 직접 쓰면 desync. 아키텍처 위반 | reject (P1b는 condition 확장으로) |
| `option_d_persistence_record_meta_classifier` | record=Persistence, 판정=Meta 순수, 집계=Unity settlement | ledger 패턴 정합, 순수 판정 테스트 가능, combat 무관 | record와 분류가 두 asmdef로 나뉨(의도된 분리) | accept |

## 결과

채택 구조의 장점:

- 전투 결과가 휘발성 컷신 소품이 아니라 save truth의 실제 상태값이 된다(GPT Pro 핵심 요구 충족).
- 분류 판정이 `SM.Meta` 순수 코어로 분리돼 Unity/combat 없이 EditMode 검증된다(`DossierOutcomeClassifierTests`).
- persistence round-trip이 `JsonPersistenceTests`에 커버된다(새 변경을 test가 잡는다).
- combat은 `BattleResult` 사실만 산출 — objective/narrative 무지 유지.

감수할 비용:

- record(Persistence)와 분류(Meta)가 두 asmdef로 나뉜다 — 의도된 분리지만 한 outcome을 두 곳에서 본다.
- P1a는 squad 생존만 본다. 민간인 구조·증거 확보 같은 objective는 combat 모델링이 붙는 P2에서 분류 확장.

## 후속 작업

1. **P1b code half (구현됨, 2026-06-04)**: outcome → story flag → Town-return 분기. 코드 grounding 후 `StoryConditionKind` 확장(Option A) 대신 **`StoryDirectorService.SetFlag`(through-director — `Progress`를 통해서만 변이, 우회/desync 아님)**를 추가하고 settlement이 costly outcome을 `story_flag_{chapterId}_squad_costly`로 stamp한다. Town-return 분기는 기존 `FlagSet` condition으로 author한다(data-driven 보존). 즉 **기계적 outcome→flag는 code(classifier 옆), narrative flag→dialogue는 manifest** — Option A보다 분리가 깨끗하다. `StoryConditionKind`/`StoryMomentContext`/pipeline 조건 파싱 변경 0. condition 확장은 outcome 조건이 늘 때 data-driven 마이그레이션으로 재검토. content half(pindoc costly overlay + event-map + 파이프라인)는 P1b-content.
2. **P2a (구현됨, 2026-06-04 → ADR-0027)**: Warrant(출격 전 서약) 판정 — 전투 사실(승패·생존·turn 수)만으로 `Kept`/`Broken` 판정, combat 0 변경. 본 ADR이 적은 "combat 모델링이 붙는 P2에서 분류 확장"의 **전투 엔티티 부분**(비살상 최종 상태·증거 오브젝트·민간인 유닛)은 ADR-0027에서 **P3**로 분리됐다(전투 모델 변경 동반). Warrant 선택 UI + faction pressure는 P2b.
3. Dossier UI surface(한새 거점에서 "내가 보낸 사람들이 무엇을 했는가") — 표시 계층.

## 작성 지침

- persistence record는 `SM.Persistence.Abstractions.Models`, 도메인 판정은 `SM.Meta` 순수 코어, 집계/영속화는 `SM.Unity` flow — 한 문장에 섞지 않는다.
- `SaveProfile`에 새 ledger 필드를 추가할 때 기본값 `new()`로 backward-compatible을 유지하고 `JsonPersistenceTests` round-trip에 커버를 추가한다.
- 전투 결과를 narrative로 흘릴 때 `SM.Combat`은 사실(`BattleResult`)만 산출하고 objective/flag 판정은 상위(Meta/Unity)에서 한다.
