# ADR-0028 정치적 Warrant 루프 — FactionState + WarrantResult (ludonarrative 루프 P2 전환)

- 상태: active
- 소유자: repository
- 최종수정일: 2026-06-04
- 소스오브트루스: `docs/04_decisions/adr-0028-political-warrant-loop.md`
- 관련문서:
  - `docs/04_decisions/adr-0027-warrant-judgment-architecture.md` (judgment rail — 본 ADR이 그 위에 정치 층을 얹는다)
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - pindoc `analysis-p2-warrant-system-design` (rev 4 — separability 실측 + GPT Pro 정치적 전환 verdict)
  - pindoc `analysis-narrative-reskin-4-faction-root-draft` (4 정치 세력 root)

## 문맥

separability 실측(`WarrantSeparabilitySimTests`)이 보였다: 현 kill-all 자동전투는 build축(Swift/Intact)도 naive protect도 **전술적 warrant tension을 못 만든다**(burst가 속도·안전 둘 다 지배, 보호대상 항상 사망). GPT Pro 전략 검수 판정(ADR-0027 참조): **warrant를 전술 약속이 아니라 정치적 mandate로 전환**한다. 선택은 "전투를 어떻게 이길까"가 아니라 "어느 세력의 기준으로 성공/실패를 판정하고 누구에게 책임지나".

GPT Pro의 "공허하지 않을 최소 필수"는 둘 다다: **FactionState 변화 + 다음 전투 조건 변화**(루프가 닫혀야 함 — 전투→정치→다음 전투). 본 ADR은 그 루프 전체를 박제한다: **slice 1**(정치 상태 + warrant→trust delta, 앞 절반) + **slice 2**(trust→다음 전투 mutation, 뒷 절반 — 루프 닫힘). 둘 다 구현·headless 검증 완료.

## 결정

- **`FactionState`는 profile truth**다 — 정치 평판은 run 간 지속되므로 run overlay가 아니라 `SaveProfile`에 둔다. `SaveProfile.FactionStanding: List<FactionStandingRecord>`, `FactionStandingRecord { FactionId, Trust }`. 다른 ledger 리스트와 동렬, 기본값 `new()` backward-compatible.
- **plumbing은 faction-id-agnostic**(string). 정치 층 코드는 어떤 faction id 문자열이든 동작한다. **실제 4 정치 세력 id 매핑은 content authoring(후속)** — pindoc `analysis-narrative-reskin-4-faction-root-draft` 기준. (주의: content의 `FactionId`(`faction_glass_forest` 등)는 **per-site enemy grouping**으로 정치 세력과는 별개 layer다. 매핑은 authoring 결정.)
- **`WarrantSpec` += `IssuerFactionId`, `OpposedFactionId`** — warrant = 어느 세력 기준을 수락(issuer)하고 누구를 거스르나(opposed).
- **판정 reuse**: 기존 fact-bag judge(`WarrantJudgment`, ADR-0027)가 faction standard를 판정한다(satisfied=Kept / failed=Broken·FailedMission). 새 판정 코어 불필요 — outcome → 정치 결과.
- **trust delta는 `FactionTrustService`(SM.Meta, 순수)**가 소유한다. satisfied → issuer +Δ, opposed −Δ; failed → issuer −Δ. EditMode 단위 검증. (betrayed/public·deniable nuance는 후속.)
- **`DossierEntryRecord` += `IssuerFactionId`, `OpposedFactionId`** — WarrantResult 영속화(누구에게 한 약속을 지켰나/깼나). 정치 outcome은 기존 `WarrantOutcome`(kept/broken/failed_mission)로 충분.
- **집계/적용은 `SM.Unity` settlement(`WriteDossierEntry`)**: warrant 판정 후 `FactionTrustService`로 delta를 계산해 `SaveProfile.FactionStanding`에 적용 + Dossier에 issuer/opposed 기록.
- **(slice 2·3) trust/standing → 다음 전투 조건은 `PoliticalCombatConditionService`(SM.Meta, 순수)**가 소유한다(slice 2 `NextCombatSupportService`를 slice 3에서 양방향으로 일반화·개명). `Resolve(pledgedWarrantId, standingLookup) → IReadOnlyList<PoliticalCombatCondition>` — 발행 세력 trust ≥ `SupportTrustThreshold`(=4)면 **AllySupport**(아군 버프), 거스른 세력 standing ≤ `AlertStandingThreshold`(=−2)면 **EnemyAlertness**(적 버프). 도출은 primitive(faction id + standing int)만 받아 `FactionTrustService`와 동일하게 SM.Meta 경계 유지.
- **(slice 3, GPT Pro #4 타입 경계) `PoliticalCombatCondition` = 다채널 + provenance**: `(SourceFactionId, Channel, ReasonCode, Package)`. `CombatModifierPackage`는 leaf(전투가 보는 일반 modifier), 출처·통로·사유는 상위에 남는다 — 정치 결과를 stat-package-only로 굳히지 않는다(roster/route 등 후속 채널이 같은 컨테이너에 붙음). `ReasonCode`는 stable token(표시 문구 아님 — ID/label 분리).
- **적용 seam(채널별)**: AllySupport는 `LoadoutCompiler.Compile(squadSupportPackages)` finalize에서 ally `NumericPackages`에 접혀 **compile hash**에 포착. EnemyAlertness는 `GameSessionState.TryResolveCurrentEncounterCore`가 resolved `context.Enemies`에 `ApplyEnemyPackages`로 접어 **EnemySnapshotHash**에 포착. 둘 다 `GameSessionState`가 `overlay.PledgedWarrantId` + `Profile.FactionStanding`(standing 읽기는 SM.Unity, 공유 helper `ResolveFactionStanding`)에서 도출해 주입. compiler/resolver는 정치를 모른다.
- **replay 주의(정직)**: 정치 조건은 출격 시점 standing에서 도출되고 각 hash에 포착돼 **live 결정적**이다. 단 match record는 loadout을 verbatim 보존하지 않고 hash+event만 남긴다 — replay 재검증이 *변동된* Profile에서 재도출하면 drift 가능. 현재 그 재검증 경로는 없다(determinism 테스트는 동일 snapshot 재실행). 생기면 resolved 조건을 per-sortie로 `BattleContextState`에 snapshot해야 한다(후속).
- **`SM.Combat` 불변.** asmdef: record=`SM.Persistence.Abstractions`, 조건 도출=`SM.Meta` 순수, 적용·standing 읽기=`SM.Unity`. ADR-0027 judgment rail 위에 정치 층만 추가.

## 검토한 대안

| option | description | verdict |
| --- | --- | --- |
| `run_overlay_faction_state` | FactionState를 run overlay에 | reject — 평판은 run 간 지속(profile-level) |
| `keep_swift_intact_tactical` | Swift/Intact build축 warrant 유지 | reject — separability가 미분리 반증(ADR-0027) |
| `immediate_full_mutation` | trust→다음 전투 mutation까지 한 슬라이스에 | defer — 슬라이스 분리(foundation 먼저, mutation 후속) |
| `faction_id_from_content_tag` | content `FactionId`(per-site)를 정치 세력으로 재사용 | reject — per-site enemy grouping과 정치 세력은 별개 layer |
| `political_state_profile + meta_delta + unity_apply` (accept) | FactionState=profile truth, delta=Meta 순수, 적용=Unity settlement | accept |

## 결과

장점: **전투→정치→전투 1 cycle이 닫혔다** — 서약 이행 → issuer trust ↑ → 임계 돌파 시 다음 출격에 지원 버프(GPT Pro "FactionState 변화 + 다음 전투 조건 변화" 둘 다 충족). judgment rail(ADR-0027) 재사용 — 새 판정 코어 0. combat 무관(compiler는 일반 package만 접고 hash가 이를 포함). faction-id-agnostic이라 content 4-faction 확정 전에 plumbing 검증 가능(P2a rail 패턴 동일).

감수할 비용: slice 2 지원 mutation은 **단일 tier·고정 magnitude placeholder**다(threshold 4, max_health+4/phys_power+2). 신뢰-비례 scaling, 부정 방향(저신뢰 → enemy_alertness 등), 세력별 차등은 후속·balance 단계. 또한 `RunLoopContract` 픽스처는 archetype이 비어(contract 전용) GameSessionState 레벨 e2e로 deployed squad를 못 띄운다 — seam 검증은 compiler-fold 테스트(실 archetype 2-ally, 서비스 산출물→fold+hash) + 서비스 단위 테스트로 커버하고, GameSessionState glue(5줄)는 양변이 테스트된 조합이다.

## GPT Pro 최종 검수 (2026-06-04) — "체크박스 충족"

slice 2(닫힌 루프)를 GPT Pro 확장 검수에 올렸다(`response-20260604-041346.md`, chat `…/c/6a207cf2`). **전체 판정: 체크박스 충족** — state는 전투 compile에 들어왔으나 현 효과가 generic ally stat buff라 *정치적 원인*으로 식별되기 약하다(진짜 피드백 아님, 함정도 아님).

행동 채택(검수 동의):
- **(#4 진짜 함정) 타입 경계**: 위험은 "support=stat package"가 아니라 *모든 정치 결과를 stat-package-only로 흘리는 것* + `trust` 단일 scalar를 모든 정치 상태의 alias로 굳히는 것. → 다채널 effect 컨테이너(`CombatModifierPackage`는 leaf, 상위는 ally/enemy/roster/route/economy 채널 보유) + provenance(SourceFactionId/Channel/UiLabel/CauseText). 되돌리기 비싼 결정이라 **지금** 고친다.
- **(#3·#5 양방향) `opposed −1`이 ledger에서 죽는다** — 정치 충돌의 절반이 다음 전투에 안 닿는다. 4세력 공동 죄 정치극에서 이게 핵심. opposed 축 → enemy alertness가 최고 체감 채널(자동전투 관전에서 시작 조건 변화는 즉시 보임).
- **(#1 가독성) 정치 의미가 compile hash 안에 숨음** — provenance를 Dossier/감사에 남겨 "어느 세력 mandate에서 왔나"가 보이게.

반박(opus 판단, 무비판 수용 아님):
- GPT Pro "상시 aura/버프 farming" 우려(#4-셋째)는 **현 설계에 미해당** — `ResolveSupportPackages`가 이미 *per-sortie pledged warrant의 issuer*로 게이트(자격=trust≥4 AND 활성=이번 출격 서약). 모든 세력 ≥4 상시 버프 아님.

보류(YAGNI/차단):
- breakpoint estimator + 출격 화면 "3타→2타" 미리보기 — P2b 선택 UI(미존재)에 차단.
- `Heat/Debt/Scandal` 다축 state, threshold→policy data — 실제 세력 확정 전 조숙(int 필드/상수→데이터는 후방호환 추가라 나중도 싸다).

## 후속 작업

- **(구현됨) slice 2 — NextCombatMutation**: `NextCombatSupportService`(SM.Meta) + `LoadoutCompiler.squadSupportPackages` seam. issuer trust ≥ T → 다음 출격 squad-wide 지원. headless 검증(서비스 단위 8 + compiler-fold 통합).
- **(구현됨) slice 3 — 양방향 정치 조건**: (A) `PoliticalCombatCondition` 다채널 타입 + provenance, slice 2 support를 AllySupport leaf로 refactor(`PoliticalCombatConditionService`로 개명). (B) opposed standing ≤ −2 → EnemyAlertness(적 buff), `context.Enemies`에 fold(EnemySnapshotHash 포착). GPT Pro #4(타입 함정)·#3·#5(양방향) 대응. headless 검증(서비스 9 + ApplyEnemyPackages fold + compiler-fold). 적 fold의 GameSessionState glue는 fixture archetype 부재로 e2e 미적용(양변 테스트된 조합). replay drift는 위 '정직' 주의 — 후속.
- **(구현됨) #5 OfferSet(거절 면)** — `WarrantOfferService.ComputeRejectionDeltas`(SM.Meta 순수): 정치 서약은 같이 제안된 다른 세력 mandate를 거절한 것 → 거절당한 세력 신뢰 −`RejectedOfferLoss`(issuer 사이드·opposed slice1 처리분 제외, 중복 방지). 결과 무관(선택 시점). settlement이 `WarrantCatalog.PoliticalWarrantIds`(placeholder offer 소스)에서 도출해 적용 + `DossierEntryRecord.RejectedFactionIds`에 영속("누구를 거절했나"). 거절 신뢰 하락은 slice 3 EnemyAlertness로 되먹임(다극 충돌 gradient). headless 검증(서비스 3 + round-trip + e2e settlement). offer 소스(per-site)는 P2b content가 교체.
- **(부분 구현) content** — 4 정치 세력 stable id(`faction_solarum`/`faction_wolfpine_tribes`/`faction_pale_conclave`/`faction_lattice_order`, reskin settled) + 4 세력 위임 warrant(`warrant_solarum_order` 등, issuer/opposed/조건) 카탈로그 authoring 완료. placeholder(council/militia) 제거. **남은 것**: content `FactionId`(per-site enemy) ↔ 정치 세력 매핑, warrant offer 소스(어느 site가 무엇을 제안하나 — P2b UI/content). 세력-warrant 대응·대립 구조 rationale은 pindoc(analysis-p2-warrant-system-design rev 9).
- **(부분 구현) P2b 선택 UI** — 로직 코어 `WarrantOptionBuilder`(헤드리스 7) + **화면 + 표시명 label layer 구축 완료**(에디터 세션): UIToolkit MVP `WarrantSelection.uxml/uss` + `WarrantSelectionViewState/View/Presenter`(RewardScreen 패턴 미러, 이름 해석 Func 주입으로 headless 테스트 가능) + `WarrantDisplayDefaults`(세력/서약 한국어 표시명) + `ContentTextResolver.GetFactionName/GetWarrantName`(StringTable 우선·defaults fallback) + 키빌더. ID/label 분리 준수. 라이브 에디터 compile clean + FastUnit Presenter 9/Display 4/OptionBuilder 7 통과. **남은 것(흐름 통합)**: 화면이 scene-based 패턴(`RuntimePanelHost`/`SceneFlow`, scene별 serialized UXML)이라 `AtlasScreenController` handoff↔`GoToBattle` 사이에 끼우려면 전용 scene 또는 overlay(VisualTreeAsset 런타임 로드/serialized 할당) + `PledgeWarrant` 호출 + PlayMode visual QA 필요 — pattern-sensitive 집중 단계. StringTable .asset entry는 다국어 단계.
- **mutation 심화·balance** — 신뢰-비례 scaling, 세력별 차등. **magnitude는 실측 검증됨**(`PoliticalConditionImpactSimTests`, GPT Pro #2): 중간 스케일 squad에서 지원(+4HP/+2pow)은 marginal tier 승률 13%→75%(+63pp)·clean victory(死 1.08→0.13), 경계(+2HP/+1pow)는 marginal 승률 13%→0%·손실↑로 **둘 다 체감**(breakpoint 넘음). 이미 결정난 전투(easy 100%승/brutal 전멸)에선 binary 무변 — 즉 "박빙 전투에서만 결정적"이라 over/underpowered 아님. GPT Pro "+2pow noise?" 우려 반증. 튜닝 불요, scaling은 선택. breakpoint 가독성 display는 P2b UI와 함께.
- betrayed/public·deniable, scandal_exposure, debt_to_faction 등 FactionState 다축 확장.
- A-lite(Guardable Objective Slot)는 특정 mission type에만(비핵심).

## 작성 지침

- 정치 평판은 profile truth(`SaveProfile`), run-scoped 아님. trust delta는 `SM.Meta` 순수, 적용은 `SM.Unity` settlement — 섞지 않는다.
- faction id는 stable id(string), 표시명은 별도 label(ID/label 분리). 코드 plumbing은 faction-id-agnostic — 특정 세력 id를 코드에 박지 않는다(content authoring 소유).
- warrant 판정은 ADR-0027 fact-bag judge 재사용 — 정치 층이 새 판정 코어를 만들지 않는다.
- `SaveProfile`에 새 정치 필드 추가 시 기본값 `new()` backward-compatible + `JsonPersistenceTests` round-trip 커버.
