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
- **(구현됨, 에디터 세션) P2b 선택 UI** — 출격 전 서약 선택이 player-visible로 섰다. **로직**: `WarrantOptionBuilder`(제안 warrant+standing → 옵션+조건 미리보기). **화면**: UIToolkit MVP `WarrantSelection.uxml/uss` + `WarrantSelectionViewState/View/Presenter`(RewardScreen 패턴, 이름 해석 Func 주입). **label**: `WarrantDisplayDefaults`(세력/서약 한국어 표시명) + `ContentTextResolver.GetFactionName/GetWarrantName`(StringTable 우선·defaults fallback) — ID/label 분리. **흐름 통합**: `AtlasScreenController.ContinueToExpedition`(battle branch, `GoToBattle` 직전) `TryShowWarrantSelection` — `RuntimePanelHost.Root`에 overlay, 카드=`PledgeWarrant(id)`+출격 / 스킵=`PledgeWarrant("")`+출격. **활성화**: `WarrantSelection.uxml/uss`를 `Atlas/Resources/`로 옮겨 `Resources.Load` 폴백(기존 repo 패턴) — scene 와이어 없이 자동 활성. asset 미할당이면 직행(fallback, 흐름 안전). **검증**(라이브 에디터): compile clean + Presenter 9/Display 4/OptionBuilder 7 + **화면 렌더 스모크 1**(Resources 로드→UXML 계약→카드 렌더). **남은 polish**: PlayMode 네비게이션 visual QA(렌더는 스모크로 검증됨, 인게임 도달 확인+스타일 튜닝), StringTable .asset(다국어), per-site offer 소스.
- **(구현됨, 에디터 세션) P2c 정치 정산 가독성** — GPT Pro #1("정치 의미가 compile hash 안에 숨음")에 대응. 출격 정치 정산(이행/거스름/거절 + 신뢰 delta)이 적용 시점에 사라져 player가 인과를 못 읽던 것을 *player-visible*로 세운다. **코어**: `PoliticalSettlementReporter`(SM.Meta 순수) — `FactionTrustService`(이행/거스름)·`WarrantOfferService`(거절)에 수치를 위임하고 line마다 사유(`PoliticalSettlementReason`: KeptIssuer/BrokenIssuer/DefiedOpposed/RejectedOffer)를 태깅해 `PoliticalSettlementReport`로 합친다(두 delta 소스는 세력 disjoint — 거절이 issuer/opposed 제외). **적용·캡처**: settlement(`WriteDossierEntry`)이 이 report를 그대로 적용 + Dossier `RejectedFactionIds` 기록 + `GameSessionState.LastPoliticalSettlement`에 보관(미서약이면 Empty → 정치 섹션 숨김). 기존 2-call(trust+거절)을 reporter 1-묶음으로 합쳐 net delta 보존(비회귀 RunLoopContract 20). **화면**: reward 화면 노출 — Summary 패널 1줄 headline(어느 세력에 한 약속 이행/위반 + 신뢰 ±N) + progression ledger에 세력별 상세 행(사유·신뢰 부호·현재 standing, gain/loss tone). UXML 무변경(기존 동적 progression row 재사용), 표시명은 label layer(`WarrantDisplayDefaults.SettlementReasonText` + `ContentTextResolver.GetFactionName`, ID/label 분리). **검증**(라이브 에디터): compile clean + Reporter 7 + RewardPoliticalRows 2 + Display reason 1 + e2e RunLoopContract 20(정산 trust/거절 비회귀) + WarrantOffer 3 + JsonPersistence 1. **남은 polish**: PlayMode 육안(렌더는 단위/스모크로 검증됨), 세력 standing 상설 뷰(Town), per-site offer 소스.
- **(구현됨) #b per-site offer 소스** — GPT Pro P2c 재검수 1순위. warrant offer를 전역 placeholder 카탈로그가 아니라 *site의 정치 맥락*에서 도출한다(warrant가 "런 시작 전 고르는 계약 카드"가 아니라 "이 장소의 정치 사건"으로 읽히게). `SiteWarrantOfferResolver`(SM.Meta 순수): `Resolve(siteId) → SiteWarrantOffer(PressureFactionId, OfferedWarrantIds, CauseCode)`. 미등록 site는 전역 정치 카탈로그 graceful fallback(비회귀). 정치 지리 seed는 reskin settled에 grounded — Wolfpine Trail=이리솔, 묘역=회상, 옛 왕도 관문=솔라룸, 격자 노드=그물(각 site는 압력 세력 위임 + 대립 위임 제안). 두 offer-source 소비자(선택 UI `AtlasScreenController`, settlement 거절 `WriteDossierEntry`)가 site-resolved로 전환 — 거절 면이 "이 장소에서 누구를 거절했나"가 된다. 선택 화면 헤더에 압력 산문 노출(label layer `WarrantDisplayDefaults.SitePressureCause`, ID/label 분리). **목적은 콘텐츠 완성도가 아니라 데이터 경로**(GPT Pro) — 전체 10-site 지리 authoring은 world bible 후속. 검증: Resolver 4 + Presenter site-offer 1 + Display cause 1 + e2e RunLoopContract 20(미등록 fixture site fallback 비회귀). commit 후속.
- **(구현됨) #provenance 전투 중 정치 marker** — GPT Pro 잔여 20%("관전 중 체감")·"슬롯 지금" 1순위. 도출된 정치 조건(AllySupport/EnemyAlertness + 출처 세력·채널·사유)을 `GameSessionState.ActiveBattlePoliticalConditions`로 보관(compile/snapshot hash가 *효과*만 포착하던 출처를 player-visible로). 전투 HUD(`BattleScreenPresenter`)가 tactical readout에 세력별 행(후원/경계 tone)을 노출 — 관전 내내 "정치가 전투에 들어왔다"가 읽힌다. `SM.Combat` 불변(조건 도출=SM.Meta, 보관·표시=SM.Unity). 채널 표시명은 label layer(`WarrantDisplayDefaults.ChannelText`, ID/label 분리). UXML 무변경(기존 동적 readout row 재사용). 검증: ReadoutCore 2 + ChannelText 1 + BattleHud/DebugFoldout 6 비회귀 + RunLoopContract 20(capture 비회귀). **남은 것**(GPT Pro): 전투 시작/주요 교전 event marker 세분화, replay 재검증 경로.
- **(구현됨) #provenance audit 영속** — P2d의 session-scoped 정치 출처를 match record에 영속한다. `PoliticalConditionAudit.Encode`(SM.Meta 순수): 조건 → `"factionId|channel|reasonCode"` stable token(표시 문구 아님 — ID/label 분리). `MatchRecordBlob.PoliticalConditions: List<string>`(기본값 `new()` backward-compat), `RecordBattleAudit`가 채운다. 이로 hash가 *효과*만 포착하던 정치 출처가 audit에 남아 replay 재검증의 입력이 된다(GPT Pro "audit에 정치 condition id가 남아야"). persistence는 primitive string만(SM.Meta 무관), 인코딩=SM.Meta, 적용=SM.Unity. 검증: Audit 2 + JsonPersistence round-trip 1. commit 후속. **남은 것**: 전투 시작/주요 교전 event marker 세분화, 변동 standing 재도출 drift 비교(replay 재검증 실 구현).
- **(구현됨) incident-centric Dossier 1차** — GPT Pro §4.6: Dossier가 faction id만 들면 "네 세력 공동 죄" 교차기록을 못 담는다. incident(전투) entry에 **구조적 세력 효과** sub-record를 박는다 — `DossierPoliticalEffectRecord(FactionId, Delta, Reason)` + `DossierEntryRecord.PoliticalEffects: List<>`. settlement이 `PoliticalSettlementReport.Lines`에서 채우고, reason은 stable token(`PoliticalSettlementReasonTokens.ToToken` — `"kept_issuer"`/`"defied_opposed"`/`"rejected_offer"` 등, ID/label 분리). 기존 Issuer/Opposed/Rejected id는 이제 coarse projection이고 `PoliticalEffects`가 "누가 얼마나 왜"의 source. persistence는 primitive(SM.Meta 무관), 인코딩=SM.Meta, 적용=SM.Unity. 검증: Token 1 + JsonPersistence round-trip 1 + e2e RunLoopContract 20(incident 효과 assert). commit 후속. **남은 것**(GPT Pro): site consequence(증거 소실 등)·future condition seed를 incident에, faction-view projection UI.
- **mutation 심화·balance** — 신뢰-비례 scaling, 세력별 차등. **magnitude는 실측 검증됨**(`PoliticalConditionImpactSimTests`, GPT Pro #2): 중간 스케일 squad에서 지원(+4HP/+2pow)은 marginal tier 승률 13%→75%(+63pp)·clean victory(死 1.08→0.13), 경계(+2HP/+1pow)는 marginal 승률 13%→0%·손실↑로 **둘 다 체감**(breakpoint 넘음). 이미 결정난 전투(easy 100%승/brutal 전멸)에선 binary 무변 — 즉 "박빙 전투에서만 결정적"이라 over/underpowered 아님. GPT Pro "+2pow noise?" 우려 반증. 튜닝 불요, scaling은 선택. breakpoint 가독성 display는 P2b UI와 함께.
- betrayed/public·deniable, scandal_exposure, debt_to_faction 등 FactionState 다축 확장.
- A-lite(Guardable Objective Slot)는 특정 mission type에만(비핵심).

## GPT Pro P2c 재검수 — #1 "80% 닫힘", 다음 = site offer (2026-06-04)

P2b(예고)+P2c(귀결)를 GPT Pro 확장 검수에 올렸다(`response-20260604-211348.md`). 판정: **#1 가독성 80% 닫힘** — accounting/UI legibility(전후 화면)는 확보, 남은 20%는 *관전 중 체감*("방금 저 교전이 이 정치 조건 때문에 바뀌었다")과 *선택의 세계 정박*. 자동전투 게임에선 그 20%가 체감의 절반. **다음 1순위 = per-site offer 소스**(위 #b로 구현 — placeholder 전역 offer면 warrant가 상점 카드처럼 보인다).

**명시적 보류(GPT Pro "지금 하지 마라")**: 신뢰-비례 scaling·세력별 perk 차등·Heat/Debt/Scandal 다축·Town 상설 뷰 대형화 — 전부 "정치 = reputation 최적화 게임"으로 굳힐 위험.

**되돌리기 비싼 "슬롯 지금 만들어라"(후속, 우선순위순)** — felt 레이어 심화 단계에서 연다:

- **combat-time provenance** *(구현됨 — #provenance: readout 노출 + audit 영속)*. **남은 것**: 전투 시작/주요 교전 event marker 세분화, replay 재검증 실 구현(변동 standing 재도출 drift 비교).
- **incident-centric Dossier** *(1차 구현 — incident에 구조적 세력 효과 sub-record `PoliticalEffects`)*. **남은 것**: site consequence(증거 소실 등)·future condition seed를 incident에, faction-view projection UI.
- **reason enum ≠ policy key**: `PoliticalSettlementReason`은 coarse action class로만. data-driven `PolicyId`/`ContentReasonCode`/`Effects[]`를 위에 얹는다. `Δtrust`를 line 중심으로 고정 말고 `Effects[]` 1급(Trust는 한 channel) — #4 함정 재발 방지.
- **promise political-claim slot**: 속전/온전에 `PoliticalClaimType` slot(세력별로 "속전"이 다르게 읽히게).
- **threshold-behind-policy**: `standing ≤ −2` hard-coded를 `FactionPolicy.Resolve…` 뒤로 숨겨 call site가 threshold를 모르게.
- **headline selector**: reward headline을 issuer 고정이 아니라 dominant consequence(issuer/opposed/site) 선택.

## 작성 지침

- 정치 평판은 profile truth(`SaveProfile`), run-scoped 아님. trust delta는 `SM.Meta` 순수, 적용은 `SM.Unity` settlement — 섞지 않는다.
- faction id는 stable id(string), 표시명은 별도 label(ID/label 분리). 코드 plumbing은 faction-id-agnostic — 특정 세력 id를 코드에 박지 않는다(content authoring 소유).
- warrant 판정은 ADR-0027 fact-bag judge 재사용 — 정치 층이 새 판정 코어를 만들지 않는다.
- `SaveProfile`에 새 정치 필드 추가 시 기본값 `new()` backward-compatible + `JsonPersistenceTests` round-trip 커버.
