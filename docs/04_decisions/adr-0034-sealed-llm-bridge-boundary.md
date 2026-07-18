# ADR-0034 sealed LLM bridge 순수 codec 경계

- 상태: active
- 소유자: repository
- 최종수정일: 2026-07-18
- 소스오브트루스: `docs/04_decisions/adr-0034-sealed-llm-bridge-boundary.md`
- 관련문서:
  - `docs/03_architecture/dependency-direction.md`
  - `docs/03_architecture/assembly-boundaries-and-persistence-ownership.md`
  - `docs/04_decisions/adr-0030-h100-headless-metrics-boundary.md`
  - `docs/04_decisions/adr-0031-h100-headless-policy-boundary.md`

## 문맥

BT1 E07b의 봉인 냉시작 LLM 브리지는 관측/액션/request를 byte-deterministic canonical form으로 봉인하고 3-프로세스 재생으로 검증한다(BT1). 그 첫 조각인 wire codec은 두 축의 타입을 동시에 만진다: `SM.HeadlessPolicies`의 player-visible 관측·결정 DTO(canonicalize 대상, 액션 decode 목적지)와 `SM.HeadlessMetrics`의 length-prefix FNV-1a 해시 codec(`LengthPrefixedStableHash`, wire 응답 serializer와 봉인 trace가 공유하는 byte contract).

두 sibling asmdef는 의도적으로 서로를 참조하지 않는다(adr-0030/0031). 두 축을 동시에 보는 기존 어셈블리는 `SM.Editor`뿐이지만, `BuildBoundaryGuardFastTests`가 `SM.Tests.FastUnit -> SM.Editor` 참조를 금지한다(FastUnit lane을 engine-free·editor-free로 닫기 위함). codec을 `SM.Editor`에 두면 FastUnit이 검증할 수 없다. 그런데 codec의 byte-determinism은 BT1 재생 정합의 기반이라 fast-lane 회귀 보호가 특히 필요하다(다른 pure Headless 로직도 전부 FastUnit 검증). 즉 "두 sibling을 조립" + "FastUnit 검증 가능" + "engine-free"를 동시에 만족하는 기존 어셈블리가 없다.

(초기 구현은 이 codec을 `SM.Editor.Validation`이라는 이름의 새 asmdef에 두었으나, 그 이름은 이미 `SM.Editor`가 소유한 H100 코드의 namespace와 겹쳐 하나의 namespace가 두 어셈블리로 쪼개졌다. HUB 구조 리뷰에서 이름·위치를 교정했다.)

## 결정

- 새 `SM.SealedLlmBridge` asmdef가 봉인 LLM wire codec을 소유한다: 관측 canonical bytes(`SealedLlmObservationCodec`/`SealedLlmObservationCanonicalWriter`), legal action descriptor + hash(`SealedLlmLegalActionSet`), strict `selected_action` encode/decode(`SealedLlmActionCodec`/`SealedLlmActionGrammar`/`SealedLlmActionDecodeException`), request preimage(`SealedLlmRequestCodec`), 공용 canonical value writer(`SealedLlmCanonicalValue`), decision envelope(`SealedLlmDecisionEnvelope`).
- 경로는 `Assets/_Game/Scripts/Editor/SealedLlmBridge/**`, namespace는 `SM.SealedLlmBridge`로 `SM.Editor.Validation` namespace와 assembly identity·namespace 모두 분리한다.
- `references`는 정확히 `SM.Combat`, `SM.HeadlessMetrics`, `SM.HeadlessPolicies`. `includePlatforms: Editor`, `noEngineReferences: true`, `autoReferenced: false`. dev/validation 전용 tooling이라 shipped runtime을 부풀리지 않도록 Editor platform으로 두되 engine 참조는 갖지 않는다.
- `SM.Editor`와 `SM.Tests.FastUnit`이 이 narrow asmdef를 참조한다. FastUnit의 `SM.Editor` 금지는 유지하고, 이 bridge는 engine-free이므로 예외적으로 직접 참조를 허용한다.
- `SM.HeadlessMetrics.LengthPrefixedStableHash`를 `public`으로 승격한다. wire 응답 serializer(HeadlessMetrics)와 bridge codec(SealedLlmBridge)이 공유하는 foundational byte codec이므로 assembly-name을 박는 `InternalsVisibleTo` 대신 공개 표면으로 공유한다. 알고리즘·byte layout은 불변이다.
- codec decode는 strict fail-closed다: off-menu key, malformed grammar, 부담불가/차단/미공개 옵션은 `SealedLlmActionDecodeException`을 던지고 silent fallback을 만들지 않는다(E07b-2b가 이를 봉인 terminal-failure로 전환).
- 배치 legal action은 조합적이라 전량 열거하지 않고 action-space descriptor(가용 heroId·anchorId·capacity)로 봉인하며 실제 합법성은 `HeadlessPolicyGuard`가 판정한다. reward/recruit/passive/refit은 작은 열거 목록으로 봉인한다.

## 검토한 대안

| 대안 | 판정 | 이유 |
| --- | --- | --- |
| codec을 `SM.Editor`에 넣고 FastUnit 대신 EditMode 테스트로 검증 | 기각 | byte-determinism 회귀를 fast-lane에서 잃는다. codec은 BT1 재생 정합의 기반이라 다른 pure Headless 로직과 같은 fast 검증이 필요하다. |
| codec을 `SM.HeadlessPolicies` 또는 `SM.HeadlessMetrics`에 추가 | 기각 | sibling은 서로를 참조하지 않는다(adr-0030/0031). codec은 둘 다 필요하므로 어느 쪽에도 들어갈 수 없다. |
| 초기안: `SM.Editor.Validation` 이름의 새 asmdef | 기각 | assembly 이름이 기존 `SM.Editor` H100 코드의 namespace와 겹쳐 namespace가 두 어셈블리로 쪼개지고 소유가 모호해진다. |
| 런타임 sibling(`SM.*Bridge`, Runtime platform) | 기각 | 이 codec은 dev/validation harness 전용이라 shipped runtime에 들어갈 이유가 없다. Editor platform이 빌드를 부풀리지 않는다. |
| 채택: `SM.SealedLlmBridge` Editor pure asmdef | 채택 | 두 sibling 조립 + FastUnit 검증 + engine-free를 동시에 만족하고 namespace 소유를 명확히 한다. |

## 결과와 영향

- 의존은 forward DAG로 닫힌다: `SM.Editor` -> `SM.SealedLlmBridge` -> {`SM.HeadlessMetrics`, `SM.HeadlessPolicies`} -> {`SM.Core`, `SM.Combat`}. 역방향·순환 없음.
- `BuildBoundaryGuardFastTests`가 `SM.SealedLlmBridge`의 `noEngineReferences` + exact references({Combat, HeadlessMetrics, HeadlessPolicies}) + `SM.Tests.FastUnit`의 이 asmdef 참조를 고정한다.
- E07b-2b(capture 어댑터 + `ISealedDecisionSource` + SyntheticStandInSource)는 이 codec 위에 build한다. 순수 어댑터/소스는 이 asmdef에, 실 LLM transport와 hook 배선은 `SM.Editor`에 남는다.
- codec logic·byte layout은 rehome 전후 불변이다(FastUnit이 byte-identity를 고정). sim/save truth와 golden은 변경하지 않는다.

## HUB 승인 (2026-07-18)

새 pure Editor asmdef 분리(두 sibling 조립 + FastUnit engine-free 검증 + namespace 소유 명확화), exact reference {Combat, HeadlessMetrics, HeadlessPolicies} + no-engine, `LengthPrefixedStableHash` public 승격(알고리즘 불변), strict decode fail-closed, 배치 descriptor + guard no-cheat 계약을 승인한다. HUB 독립 재검증: test-batch-fast 1160/1156/0(FastUnit + codec 8종 + 경계 가드), test-harness-lint 7/7, docs-policy/docs-check 통과, `git grep`로 구 assembly-identity 참조 0 확인. codec fidelity는 25개 관측 타입 전 public property를 reflection으로 강제하는 FastUnit이 고정한다. 상태 `active`.
