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

- **`WarrantKind` / `WarrantSpec` / `WarrantJudge` / `WarrantJudgment` / `BattleFactSet`는 `SM.Meta`가 소유**한다 (`DossierOutcomeClassifier` 옆, 순수 static/record, engine 무참조 — EditMode 단위 검증 가능). **판정 signature는 fact-bag 기반**: `WarrantJudge.Judge(WarrantSpec? spec, BattleFactSet facts, EncounterContext context) → WarrantJudgment`. `BattleFactSet`(전투 objective-agnostic 사실)은 SM.Unity settlement이 `BattleResult`에서 조립해 넘긴다(combat은 `BattleFactSet`을 모른다). `WarrantJudgment`은 outcome만이 아니라 `FailureReason`·`Severity`·`ObservedTurnCount`·`ResolvedTurnLimit`을 운반(reason-bearing). 이 fact-bag/judgment 구조는 GPT Pro 검수(§5.1/§5.2) 반영이며, P3(Protect/Evidence/NonLethal)에서 WarrantKind 케이스 + BattleFactSet 필드만 늘면 signature가 보존된다.
- **슬라이스 1 `WarrantKind`** (기존 사실만으로 판정):
  - `Swift` (속전): 승리 && `stepCount <= Threshold` → `Kept`.
  - `Intact` (온전): 승리 && `survivorAllyCount == totalAllyCount` → `Kept`.
  - `None`: 미서약 → `NotApplicable`.
  - 서약이 있는데 패배 → `Broken`(약속한 임무를 못 가져왔다). 승리했지만 조건 미달 → `Broken`.
- **서약 id는 per-sortie truth로 run overlay에 실린다**: `RunOverlayState.PledgedWarrantId`(runtime) ↔ `ActiveRunRecord.PledgedWarrantId`(persistence). `RewardSourceId`/`BattleContextHash`와 **동렬·동일 rail**(같은 sync 지점 `SessionProfileSync` record↔state 2곳, 같은 `Overlay with` 변이 패턴). 기본값 `""` — backward-compatible.
- **`DossierEntryRecord`에 reason-bearing 서약 snapshot 추가** (`WarrantId`, `WarrantOutcome`, `WarrantFailureReason`, `WarrantSeverity`, `WarrantObservedTurnCount`, `WarrantResolvedTurnLimit`). P1의 string-token 패턴 + 관측 사실. "깼다"가 아니라 "왜·얼마나 깼나"를 영구 보존해 결과창·Dossier UI 재구성과 세력 반응 차등을 가능케 한다(GPT Pro §5.3). 기본값 `""`/`0` — 구 세이브 호환. **Dossier가 source of truth**이고, `story_flag_{chapterId}_warrant_{kept|broken}`은 authored dialogue용 coarse projection일 뿐이다(§5.4 — systemic 효과는 P2b에서 Dossier/FactionState로 물린다).
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

## GPT Pro 검수 반영 (P2a 모델 일반화, 2026-06-04)

GPT Pro 확장 검수(`.gptprosubmit/payload/response-20260604-015138.md`) 판정: **rail(저장·판정 spine)은 합격, 그러나 P2 시스템 설계는 아직 미합격.** 단일 핵심 위험 — **자동전투(플레이어가 배치·택틱만 정하고 관전)에서 `turn 수`/`squad 손실`이 출격 전 선택으로 충분히 분리되지 않으면 Swift/Intact는 선택이 아니라 결과에 붙는 사후 라벨**이 된다. P2의 단 하나의 핵심 문제: **"출격 전 서약이 플레이어의 준비 선택을 실제로 바꾸는가"** (Warrant = result label이 아니라 출격 전 opportunity cost).

본 turn에 반영한 **되돌리기 비싼 구조 수정**(§5, 코드):

- **§5.1 fact-bag judge**: `Judge(spec, victory, survivor, total, step)` → `Judge(spec, BattleFactSet, EncounterContext)`. P3에서 새 전투 사실이 붙어도 signature 보존.
- **§5.2 reason-bearing judgment**: `WarrantOutcome` 단일 enum → `WarrantJudgment`(Outcome+FailureReason+Severity+관측사실). 패배는 `FailedMission`으로 단순 Broken과 구분.
- **§5.3 Dossier fact snapshot**: 위반 원인·관측 turn·resolved 임계 영속.
- **§5.4 flag = projection**: Dossier가 SoT, flag는 dialogue 게이트용. settlement 주석으로 박제.
- **§5.7 "0 combat change" 재구성**: 영구 원칙이 아니라 P2a 슬라이스 제약. 올바른 원칙은 "combat은 narrative를 모르지만 필요한 fact는 산출한다".

**P2b로 미룬 것**(GPT Pro 권장 순서 — 모두 P2a rail 위에 add):

1. **분리 가능성 검증(separability sim)** — Swift-build vs Intact-build가 실제로 다른 turn/casualty 분포를 내는지 측정. 이게 통과 안 되면 Swift/Intact 문법 자체 재검토(가장 먼저).
2. **Warrant ↔ tactic/posture 결합** — Swift=Aggressive 계열만, Intact=Guarded 계열만 선택 가능하게 묶어 출격 전 행동 계약화. 전투 코드 변경 없이 가능.
3. **risk-contract UI** — 선택 버튼이 아니라 예상 turn/casualty risk + 추천 roster/tactic + 정치 보상·비용을 사전 제시.
4. **FactionEffectProfile + 비대사 상태** — 대사 분기와 함께 최소 하나의 systemic 효과(faction standing/price/recruit/route/pressure)를 Dossier로 물린다.
5. **OfferSet 기록** — `PledgedWarrantId`(택한 것)뿐 아니라 거절한 세력 기준(offerSet)을 Dossier에 남겨 "누구 편을 들었나"에 세력이 반응하게.
6. **encounter 분류 노출 제한** — Swift/Intact가 실제로 갈라지는 encounter에만 warrant 노출(dominated/trivial 전투엔 미노출).

### separability 실측 결과 (2026-06-04) — roadmap 재정렬

위 #1(separability sim)을 실제로 돌렸다(`WarrantSeparabilitySimTests`, BatchOnly, 속전 glass-cannon vs 온전 tanky-DPS × 적 3 tier × 40 seed). **결과: 어느 tier에서도 "온전이 더 느리지만 더 안전"이 안 나온다.** burst가 속도·안전 두 축을 모두 지배한다 — 죽은 적은 피해를 안 주므로 빠른 처치가 곧 최선의 방어(속도-안전이 trade-off가 아니라 정상관). 저DPS 탱크는 timeout(easy)/전멸(hard)로 strictly dominated. GPT Pro §1 row-6 reversal의 실증.

**함의: warrant tension은 build축(Swift/Intact)에서 못 온다.** 추가로 naive protect(fragile 보호대상)도 측정했으나 보호대상 생존 0%(전 posture·전 적유형) — 단순 entity로도 tension이 안 생긴다.

### GPT Pro 전략 검수 → 정치적 전환 (2026-06-04)

위 두 실측을 들고 GPT Pro 확장 검수에 전략적 fork(전술 R&D vs 정치적 reframe)를 물었다(`response-20260604-025248.md`). **판정: 정치적 reframe.**

- **warrant = faction-issued political mandate**로 전환한다. 선택은 "전투를 어떻게 이길까"(전술)가 아니라 "어느 세력의 기준으로 성공/실패를 판정하고 누구에게 책임지나"(정치). 전투는 단순 fact만 산출, warrant+FactionState가 정치 해석.
- **build축 tactical warrant(Swift/Intact를 build로 판정) 중단** — 현 combat 모델에선 거짓 선택. fact-bag judge·Dossier rail은 **유지**(정확히 이 방향에 맞음 — 전투가 fact, 상위가 정치 해석). 폐기되는 건 의미 framing뿐.
- **공허하지 않을 최소 필수(둘 다)**: `FactionState` 변화 + **다음 전투 조건 변화**(루프가 닫혀야 함: 전투→정치→다음 전투). 대사·가격 단독은 flavor.
- **정치 warrant 아키텍처**(신규, 별도 ADR로 구현 시 박제): `FactionState`(trust/fear/scandal/debt, save truth) + `WarrantResult`(issuer/opposed faction·satisfied/failed/betrayed) + `NextCombatMutation`(reinforcement/alertness/hire_cost/roster_restriction/timer/support).
- A(전술 R&D)는 비효율 — 굳이 하면 Guardable Objective Slot(DPS-vs-보호 allocation) 하나만 mission-specific A-lite(3순위).

상세·근거: pindoc `analysis-p2-warrant-system-design`(rev 4). 본 ADR-0027의 "Swift/Intact tactical" framing은 이 전환으로 supersede되나 judgment rail(fact-bag judge/Dossier/overlay)은 정치 warrant의 토대로 살아남는다.

## 작성 지침

- warrant 판정은 `SM.Meta` 순수, 전달은 overlay rail(`RewardSourceId` 동렬), 집계/stamp는 `SM.Unity` settlement — 한 문장/한 파일에 섞지 않는다.
- `SM.Combat`은 `BattleResult` 사실만 산출 — 서약/objective 판정을 combat에 넣지 않는다.
- 서약은 stable id(`warrant_swift`), 표시명은 별도 label(localization) — ID/label 분리. 코드 id에 lore 어휘 박지 않는다.
- overlay에 per-sortie 필드를 추가할 때 `RunOverlayState`(optional 기본값) + `ActiveRunRecord`(기본 `""`) + `SessionProfileSync` 양방향 sync 2곳을 같은 작업 단위에서 갱신한다.
