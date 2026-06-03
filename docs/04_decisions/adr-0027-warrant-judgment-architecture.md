# ADR-0027 Warrant 판정 아키텍처 (ludonarrative 루프 P2a)

- 상태: active
- 소유자: repository
- 최종수정일: 2026-06-04
- 소스오브트루스: `docs/04_decisions/adr-0027-warrant-judgment-architecture.md`
- 관련문서:
  - `docs/04_decisions/adr-0026-dossier-persistence-schema.md` (P1 "전투 → 기록" 절반 — 본 ADR이 그 앞단을 연다)
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/03_architecture/dependency-direction.md`
  - pindoc `analysis-p2-warrant-system-design` (Warrant 문법 + 세력 의미 + 슬라이스 staging)
  - pindoc `analysis-ludonarrative-loop-implementation` (루프 전체 설계)

## 문맥

ludonarrative 루프의 "전투 → 기록"(P1: `DossierEntryRecord`)에 이어 **앞단 "서약 → 출격"**을 연다. GPT Pro 검수의 단일 핵심 — 전투가 정치극 사이 콘텐츠가 아니라 정치극의 *원인* — 을 완성하려면, 전투에 들어가기 *전에* 무엇이 걸려 있었는지(squad가 어떤 세력의 기준에 서약했는지)가 있어야 한다. 그래야 전투 결과가 "그 약속을 지켰나/깼나"라는 정치적 의미를 획득한다.

핵심 제약은 **combat 순수성**(ADR-0006). 비살상 제압·증거 확보·민간인 보호 같은 objective는 전투 엔티티 모델 변경(민간인 유닛, 증거 오브젝트, 비살상 최종 상태)이 필요하다. 그러나 "약속한 기준을 실제로 지켰나"의 **1차 축은 전투가 이미 산출하는 사실만으로 판정 가능**하다: 승패, squad 생존 수, turn 수(`BattleResult.Winner` / `FinalUnits` / `StepCount`). 이 세 사실은 `SessionRewardSettlementFlow.MarkBattleResolved(victory, stepCount, eventCount, finalUnits)`에 **이미 전달된다**(P1의 `WriteDossierEntry`가 `finalUnits`를 이미 읽는다). 즉 새 데이터 수집 경로도, combat 변경도 필요 없다.

따라서 P2a는 전투 엔티티 모델을 건드리지 않고 **SM.Meta 순수 판정 + 기존 overlay rail**로 서약 절반을 닫는다. 전투 엔티티가 필요한 objective(민간인·증거·비살상)는 별도 단계(P3)로 분리한다.

## 결정

출격 전 서약(Warrant)을 정의·운반·판정·영속화하는 구조를 추가한다.

구체적으로:

- **`WarrantKind` / `WarrantSpec` / `WarrantOutcome` / `WarrantJudge`는 `SM.Meta`가 소유**한다 (`DossierOutcomeClassifier` 옆, 순수 static, engine 무참조 — EditMode 단위 검증 가능). `WarrantJudge.Judge(spec, victory, survivorAllyCount, totalAllyCount, stepCount) → WarrantOutcome`.
- **슬라이스 1 `WarrantKind`** (기존 사실만으로 판정):
  - `Swift` (속전): 승리 && `stepCount <= Threshold` → `Kept`.
  - `Intact` (온전): 승리 && `survivorAllyCount == totalAllyCount` → `Kept`.
  - `None`: 미서약 → `NotApplicable`.
  - 서약이 있는데 패배 → `Broken`(약속한 임무를 못 가져왔다). 승리했지만 조건 미달 → `Broken`.
- **서약 id는 per-sortie truth로 run overlay에 실린다**: `RunOverlayState.PledgedWarrantId`(runtime) ↔ `ActiveRunRecord.PledgedWarrantId`(persistence). `RewardSourceId`/`BattleContextHash`와 **동렬·동일 rail**(같은 sync 지점 `SessionProfileSync` record↔state 2곳, 같은 `Overlay with` 변이 패턴). 기본값 `""` — backward-compatible.
- **`DossierEntryRecord`에 `WarrantId` + `WarrantOutcome`(string token) 추가**. P1의 `Result`/`Outcome` string-token 패턴과 동일. 기본값 `""` — 구 세이브 호환.
- **집계/판정 호출은 `SM.Unity` settlement(`WriteDossierEntry`)**가 수행한다: overlay의 `PledgedWarrantId`로 `WarrantSpec`을 조회해 `WarrantJudge`를 호출하고, 결과를 Dossier entry에 기록한 뒤, 서약 결과를 chapter-scoped story flag(`story_flag_{chapterId}_warrant_{kept|broken}`)로 stamp한다 (P1b와 동일한 through-director 패턴 — `StoryDirectorService.SetFlag`, 우회/desync 아님).
- **`SM.Combat` 불변** — `BattleResult`(승패·`FinalUnits`·`StepCount`) 사실만 산출. objective/warrant 무지 유지.

## 검토한 대안

| option | description | pros | cons | verdict |
| --- | --- | --- | --- | --- |
| `option_a_combat_wincondition` | combat이 warrant별 승리조건을 평가 | 판정 1곳 | combat이 narrative/meta 개념(서약)을 앎 — 순수성 위반(ADR-0006) | reject |
| `option_b_authored_encounter_now` | 슬라이스1에서 encounter content에 `WarrantId` 박기 | live에서 즉시 mandate | content schema + importer 변경 — 첫 슬라이스 비대, 검증 표면 증가 | defer (P2b/authoring) |
| `option_c_code_site_warrant_map` | 코드에 site→warrant 하드맵 | 플럼빙 적음 | lore 어휘를 코드 id에 박음 — ID/label 분리 원칙 위반 | reject |
| `option_d_overlay_pledge_meta_judge` | 서약=overlay rail(RewardSourceId 동렬), 판정=Meta 순수, 집계=Unity, combat 무관 | rail 재사용(수집경로 0), 순수 판정 테스트, combat 0변경, P1과 대칭 | 슬라이스1은 default none으로 ships dark(선택 source는 P2b) | accept |

## 결과

채택 구조(option_d)의 장점:

- **combat 0 변경** — `BattleResult` 사실만으로 판정, 순수성 보존.
- 판정이 `SM.Meta` 순수 코어로 분리돼 Unity/combat 없이 EditMode 검증된다(`WarrantJudgeTests`).
- 기존 overlay/persistence rail(`RewardSourceId` 패턴)을 재사용 — 새 수집 경로 0, sync 지점 재사용.
- P1과 대칭(record=Persistence, 판정=Meta 순수, 집계=Unity settlement) — 인지 부하 최소.
- 슬라이스1이 `warrant=none`으로 ships dark — live 동작 무변경(안전), P2b가 선택/페이오프로 점등.

감수할 비용:

- 슬라이스1은 live에서 서약 source가 없다(default none). 선택 UI/authoring은 P2b.
- `Swift`/`Intact`는 기존 전투 사실의 *lens*다 — "다른 전투 방식을 강제"하지는 않는다. 그 강제(보호 대상이 죽으면 실패 등)는 전투 엔티티가 필요한 P3 objective.
- 서약 outcome을 Dossier(Persistence)와 판정(Meta) 두 asmdef에서 본다 — ADR-0026과 동일한 의도된 분리.

## 후속 작업

1. **P2b**: Warrant 선택 surface(출격 전 — squad가 어느 세력 기준에 서약하나) + 선택→`Overlay.PledgedWarrantId` stamp + 세력 반응 dialogue 분기(content, pindoc + event-map). `Swift`↔`Intact` 긴장(속전은 손실 risk, 온전은 지연 risk)이 "어느 세력을 만족시키나"의 정치 선택이 된다.
2. **P3**: combat objective 엔티티 — 민간인 유닛·증거 오브젝트·비살상 최종 상태. 전투 모델 변경 동반(`CombatEntityKind` 확장 또는 중립 side). 이때 `WarrantKind` 확장(`Protect`/`Evidence`/`NonLethal`)이 `WarrantJudge`에 붙고 `BattleResult`가 새 사실을 산출한다. ADR-0026이 "P2에서 분류 확장"이라 적은 부분이 정확히 이 P3.
3. Dossier/Warrant UI surface(서약 이력 표시 — "내가 약속하고 무엇을 지켰나").

## 작성 지침

- warrant 판정은 `SM.Meta` 순수, 전달은 overlay rail(`RewardSourceId` 동렬), 집계/stamp는 `SM.Unity` settlement — 한 문장/한 파일에 섞지 않는다.
- `SM.Combat`은 `BattleResult` 사실만 산출 — 서약/objective 판정을 combat에 넣지 않는다.
- 서약은 stable id(`warrant_swift`), 표시명은 별도 label(localization) — ID/label 분리. 코드 id에 lore 어휘 박지 않는다.
- overlay에 per-sortie 필드를 추가할 때 `RunOverlayState`(optional 기본값) + `ActiveRunRecord`(기본 `""`) + `SessionProfileSync` 양방향 sync 2곳을 같은 작업 단위에서 갱신한다.
