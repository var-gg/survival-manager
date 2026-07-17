# ADR-0033 H100 Town 로스터 결정 표면

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-17
- 소스오브트루스: `docs/04_decisions/adr-0033-h100-roster-decision-surface.md`
- 관련문서:
  - `docs/03_architecture/h100-headless-policy-contract.md`
  - `docs/03_architecture/h100-headless-metrics-contract.md`
  - `docs/03_architecture/h100-build-space-census-contract.md`
  - `docs/04_decisions/adr-0031-h100-headless-policy-boundary.md`
  - `docs/04_decisions/adr-0032-h100-build-space-census-boundary.md`

## 문맥

BT1-E05의 배치·보상 전용 intent-track은 영입, 패시브 노드, Refit이 필요한 variant를 `lever_pending`으로만 분류했다. 이 세 행동은 플레이어가 Town UI에서 이미 수행하고 `GameSessionState`의 공개 session API가 소유하지만, H100 정책 경계에는 결정 표면이 없었다. 그 결과 실제 게임에 존재하는 agency와 evaluator가 관찰하는 agency가 달랐다.

기존 `IHeadlessPolicy`에 메서드를 추가하면 여섯 production 정책의 계약과 RC1 비교 기준이 동시에 바뀐다. 반대로 Town 규칙을 정책 assembly에 복제하면 비용, 로스터 cap, 패시브 합법성, Refit 결과의 source-of-truth가 둘로 갈라진다.

## 결정

- `IHeadlessPolicy`의 배치·보상 시그니처는 변경하지 않는다.
- 같은 pure `SM.HeadlessPolicies` asmdef에 opt-in `IHeadlessRosterPolicy`를 둔다. 이 인터페이스는 현재 Town observation만 받아 영입, 패시브 노드, Refit 결정을 반환한다.
- `ConceptCommitPolicy`와 그 preview-grounded 생성 모드만 opt-in 인터페이스를 구현한다. 기존 여섯 production 정책은 구현하지 않으며 runner는 해당 정책의 Town 결정 창을 생성하지 않는다.
- `SM.Editor.Validation`이 현재 영입 오퍼 네 장, 현재 Town 로스터, 패시브 보드·예산·선행 조건·keystone, 현재 item·affix slot·비용을 pure DTO로 투영한다. 미래 영입 오퍼와 Refit 결과는 정책에 공개하지 않는다.
- 사이트 보상 정산 뒤 Town으로 돌아온 시점에 영입 → 패시브 노드 → Refit 순서로 결정한다. 각 단계 뒤 observation을 다시 만들며, 실행은 기존 `Recruit`, `SelectPassiveBoard`/`TogglePassiveNode`, `RefitItem` session API만 호출한다.
- `HeadlessRosterPolicyGuard`는 현재 wallet, 로스터 cap, 노드 예산·선행 조건·상호배제·keystone cap, Refit slot과 비용, rationale·finite value·evidence를 fail closed한다. 실제 session API가 최종 게임 규칙 권위를 계속 가진다.
- E01 fact ledger와 E04 intent trace에 세 Town 결정을 추가한다. 영입과 노드의 선언 milestone 진전, 실제 gold·passive budget·echo 소비를 희소 자원 투자로 기록한다.
- `SM.HeadlessCensus`의 evaluator-only DTO는 roster capacity와 세 자원을 additive하게 운반한다. Editor adapter는 확정된 현재 선택지만 낮추고, 계약과 무관한 소비 선택을 동치 상태로 접어 탐색한다.
- asmdef와 참조 방향은 바꾸지 않는다. `SM.HeadlessPolicies`는 계속 `SM.Combat`만 참조하고, session/content 조립은 `SM.Editor.Validation`에 남는다.

## 검토한 대안

| 대안 | 판정 | 이유 |
| --- | --- | --- |
| `IHeadlessPolicy`에 Town 메서드를 직접 추가 | 기각 | 기존 여섯 production 구현과 RC1 정책 비교 계약을 동시에 깨뜨린다. |
| 여섯 production 정책 모두에 기본 Town 행동 추가 | 기각 | 기존 지표 무회귀를 입증할 기준선이 사라지고 미요청 행동이 생긴다. |
| Town 비용·합법성 규칙을 policy assembly에 재구현 | 기각 | gameplay truth가 복제되고 향후 수치 변경과 즉시 어긋난다. |
| opt-in interface와 Editor projection/session adapter | 채택 | 기존 정책 표면과 의존 방향을 보존하면서 실제 player agency만 추가한다. |

## 결과와 영향

- opt-in 정책은 배치 시 Town 전체 로스터를 볼 수 있지만 기존 여섯 정책의 observation은 기존 expedition squad 범위를 유지한다.
- Refit 정책은 실행 여부와 현재 item/slot만 고른다. 새 affix는 session API 실행 뒤에만 알려지며 선택 시점 evidence에 포함되지 않는다.
- Reroll, Scout, Retrain, Dismiss는 이 결정의 범위 밖이다.
- sim, save schema, authored content, 비용 수치와 `BattleHashCorpus` golden은 변경하지 않는다.
- intent-track 실측은 `deployment,reward,recruit,level_node,refit`을 명시해 10 anchor×16 seed를 다시 측정하고, 종전 `lever_pending`을 `v1_track` 또는 현재 horizon의 `true_unavailable`로 재분류한다.

## 승인 조건

HUB가 인터페이스 opt-in 경계, 기존 여섯 정책의 Town 창 미생성, UI-parity observation과 no-cheat Refit, session API 단일 권위, asmdef 참조 무변을 구조 리뷰한다. FastUnit witness, 10×16 intent-track 재측정, BT6/BT7와 희소 자원 투자 실측, RC1 무회귀를 독립 검증한 뒤 상태 승격 여부를 결정한다.

## HUB 승인 (2026-07-17)

구조 리뷰 통과: (1) `IHeadlessPolicy` 시그니처·여섯 production 정책·RC1 비교 기준 불변 — witness `ExistingSixProductionPolicies_DoNotOptIntoRosterWindow` 확인 (2) Town 규칙 재구현 대신 세션 API 단일 권위 유지 — 기각 대안 표가 정확 (3) 관측 UI-parity + 미래 오퍼·Refit 결과 비노출(no-cheat) (4) asmdef 참조 무변, guard fail-closed. 독립 검증: test-batch-fast HUB 재실행 일치, 재측정에서 lever_pending 1056→0 재분류 실증. proposed → active 승격.
