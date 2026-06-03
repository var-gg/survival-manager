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

GPT Pro의 "공허하지 않을 최소 필수"는 둘 다다: **FactionState 변화 + 다음 전투 조건 변화**(루프가 닫혀야 함 — 전투→정치→다음 전투). 본 ADR은 그 루프의 **앞 절반(정치 상태 + warrant→trust delta)**의 persistence/architecture를 박제한다. 뒷 절반(trust→다음 전투 mutation)은 후속 슬라이스(§후속).

## 결정

- **`FactionState`는 profile truth**다 — 정치 평판은 run 간 지속되므로 run overlay가 아니라 `SaveProfile`에 둔다. `SaveProfile.FactionStanding: List<FactionStandingRecord>`, `FactionStandingRecord { FactionId, Trust }`. 다른 ledger 리스트와 동렬, 기본값 `new()` backward-compatible.
- **plumbing은 faction-id-agnostic**(string). 정치 층 코드는 어떤 faction id 문자열이든 동작한다. **실제 4 정치 세력 id 매핑은 content authoring(후속)** — pindoc `analysis-narrative-reskin-4-faction-root-draft` 기준. (주의: content의 `FactionId`(`faction_glass_forest` 등)는 **per-site enemy grouping**으로 정치 세력과는 별개 layer다. 매핑은 authoring 결정.)
- **`WarrantSpec` += `IssuerFactionId`, `OpposedFactionId`** — warrant = 어느 세력 기준을 수락(issuer)하고 누구를 거스르나(opposed).
- **판정 reuse**: 기존 fact-bag judge(`WarrantJudgment`, ADR-0027)가 faction standard를 판정한다(satisfied=Kept / failed=Broken·FailedMission). 새 판정 코어 불필요 — outcome → 정치 결과.
- **trust delta는 `FactionTrustService`(SM.Meta, 순수)**가 소유한다. satisfied → issuer +Δ, opposed −Δ; failed → issuer −Δ. EditMode 단위 검증. (betrayed/public·deniable nuance는 후속.)
- **`DossierEntryRecord` += `IssuerFactionId`, `OpposedFactionId`** — WarrantResult 영속화(누구에게 한 약속을 지켰나/깼나). 정치 outcome은 기존 `WarrantOutcome`(kept/broken/failed_mission)로 충분.
- **집계/적용은 `SM.Unity` settlement(`WriteDossierEntry`)**: warrant 판정 후 `FactionTrustService`로 delta를 계산해 `SaveProfile.FactionStanding`에 적용 + Dossier에 issuer/opposed 기록.
- **`SM.Combat` 불변.** asmdef: record=`SM.Persistence.Abstractions`, delta=`SM.Meta` 순수, 적용=`SM.Unity`. ADR-0027 judgment rail 위에 정치 층만 추가.

## 검토한 대안

| option | description | verdict |
| --- | --- | --- |
| `run_overlay_faction_state` | FactionState를 run overlay에 | reject — 평판은 run 간 지속(profile-level) |
| `keep_swift_intact_tactical` | Swift/Intact build축 warrant 유지 | reject — separability가 미분리 반증(ADR-0027) |
| `immediate_full_mutation` | trust→다음 전투 mutation까지 한 슬라이스에 | defer — 슬라이스 분리(foundation 먼저, mutation 후속) |
| `faction_id_from_content_tag` | content `FactionId`(per-site)를 정치 세력으로 재사용 | reject — per-site enemy grouping과 정치 세력은 별개 layer |
| `political_state_profile + meta_delta + unity_apply` (accept) | FactionState=profile truth, delta=Meta 순수, 적용=Unity settlement | accept |

## 결과

장점: 정치 상태 토대 + warrant→trust 절반이 headless-testable로 선다. judgment rail(ADR-0027) 재사용 — 새 판정 코어 0. combat 무관. faction-id-agnostic이라 content 4-faction 확정 전에 plumbing 검증 가능(P2a rail 패턴 동일).

감수할 비용: **slice 1은 미완 루프다** — trust가 다음 전투를 아직 안 바꾼다(GPT Pro "다음 전투 조건 변화" 미충족). 이게 닫혀야(slice 2) 정치 warrant가 flavor를 넘는다. slice 1은 명시적으로 foundation일 뿐.

## 후속 작업

1. **slice 2 — NextCombatMutation**: `FactionState`(trust)를 battle-context-build에서 읽어 **다음 전투 조건 1개**를 바꾼다(예: issuer trust ≥ T → `starting_support` 소폭 버프, 또는 trust 낮으면 `enemy_alertness`). 버프는 loadout/setup에 적용(combat 순수성 유지). 이걸로 "전투→정치→전투" 1 cycle이 닫힌다.
2. **content** — 4 정치 세력 stable id 확정(narrative reskin 기준) + warrant authoring(issuer/opposed/조건/deltas). content `FactionId`(per-site) ↔ 정치 세력 매핑.
3. betrayed/public·deniable, scandal_exposure, debt_to_faction 등 FactionState 확장.
4. A-lite(Guardable Objective Slot)는 특정 mission type에만(3순위, 비핵심).

## 작성 지침

- 정치 평판은 profile truth(`SaveProfile`), run-scoped 아님. trust delta는 `SM.Meta` 순수, 적용은 `SM.Unity` settlement — 섞지 않는다.
- faction id는 stable id(string), 표시명은 별도 label(ID/label 분리). 코드 plumbing은 faction-id-agnostic — 특정 세력 id를 코드에 박지 않는다(content authoring 소유).
- warrant 판정은 ADR-0027 fact-bag judge 재사용 — 정치 층이 새 판정 코어를 만들지 않는다.
- `SaveProfile`에 새 정치 필드 추가 시 기본값 `new()` backward-compatible + `JsonPersistenceTests` round-trip 커버.
