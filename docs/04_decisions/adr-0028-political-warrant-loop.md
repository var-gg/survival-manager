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
- **(slice 2) trust → 다음 전투 mutation은 `NextCombatSupportService`(SM.Meta, 순수)**가 소유한다: 서약 발행 세력과의 trust ≥ `SupportTrustThreshold`(=4, 서약 2회 이행)이면 다음 출격에 squad-wide 지원 package(max_health/phys_power Flat 소폭)를 낸다. issuer 없음/저신뢰면 빈 결과. 도출은 primitive(issuer id + trust int)만 받아 `FactionTrustService`와 동일하게 SM.Meta 경계 유지.
- **(slice 2) 적용 seam은 `LoadoutCompiler.Compile`**: 옵션 파라미터 `squadSupportPackages`를 finalize 단계에서 각 ally `NumericPackages`에 접는다 → **compile hash가 지원을 포함**(replay/audit 무결성). compiler는 정치를 모른다(일반 package만 접음). `GameSessionState.BuildBattleLoadoutSnapshotCore`가 `overlay.PledgedWarrantId` + `Profile.FactionStanding`(trust 읽기는 SM.Unity)에서 도출해 주입.
- **`SM.Combat` 불변.** asmdef: record=`SM.Persistence.Abstractions`, delta/mutation 도출=`SM.Meta` 순수, 적용·trust 읽기=`SM.Unity`. ADR-0027 judgment rail 위에 정치 층만 추가.

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
1. **slice 3 — 양방향 정치 조건(다음)**: (A) 다채널 정치 effect 타입 + provenance, slice 2 support를 그 leaf로 refactor. (B) opposed 축 → enemy alertness — opposed standing ≤ 임계면 다음 전투 적에 alert package. **선행 grounding 필수**: 적 loadout(`BattleSetupBuilder`/`EncounterResolutionService`)은 ally `CompileHash` 같은 replay 해시가 없다 — enemy 정치 buff의 replay/determinism 포착 경로(battle context/record에 싣기, `PledgedWarrantId` 패턴)부터 잡고 구현. A+B는 한 묶음(B가 A 컨테이너에 꽂힘).
2. **content** — 4 정치 세력 stable id 확정(narrative reskin 기준) + warrant authoring(issuer/opposed/조건/deltas). content `FactionId`(per-site) ↔ 정치 세력 매핑.
3. **mutation 심화·balance** — 신뢰-비례 scaling, 세력별 차등, magnitude 튜닝(현 placeholder: threshold 4, +4/+2). breakpoint 가독성(P2b UI와 함께).
4. betrayed/public·deniable, scandal_exposure, debt_to_faction 등 FactionState 다축 확장.
5. A-lite(Guardable Objective Slot)는 특정 mission type에만(비핵심).

## 작성 지침

- 정치 평판은 profile truth(`SaveProfile`), run-scoped 아님. trust delta는 `SM.Meta` 순수, 적용은 `SM.Unity` settlement — 섞지 않는다.
- faction id는 stable id(string), 표시명은 별도 label(ID/label 분리). 코드 plumbing은 faction-id-agnostic — 특정 세력 id를 코드에 박지 않는다(content authoring 소유).
- warrant 판정은 ADR-0027 fact-bag judge 재사용 — 정치 층이 새 판정 코어를 만들지 않는다.
- `SaveProfile`에 새 정치 필드 추가 시 기본값 `new()` backward-compatible + `JsonPersistenceTests` round-trip 커버.
