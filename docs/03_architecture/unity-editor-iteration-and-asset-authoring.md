# Unity editor iteration and asset authoring

- 상태: active
- 소유자: repository
- 최종수정일: 2026-03-31
- 소스오브트루스: `docs/03_architecture/unity-editor-iteration-and-asset-authoring.md`
- 관련문서:
  - `docs/03_architecture/unity-agent-harness-contract.md`
  - `docs/03_architecture/content-authoring-and-balance-data.md`
  - `docs/03_architecture/editor-sandbox-tooling.md`
  - `docs/03_architecture/unity-mcp-tooling-contract.md`

## 목적

이 문서는 Unity editor에서 code loop와 asset authoring loop를 분리하고, 비싼 refresh / reload 비용을 phase gate로 통제하는 규칙을 정의한다.

## Editor state preflight

asset authoring이나 menu execution 전에 아래를 모두 확인한다.

1. 현재 Editor가 Play Mode가 아닌가
2. 현재 compile blocker가 0인가
3. 이번 sprint에서 건드릴 asmdef와 의존 방향이 문서에 적혀 있는가
4. validator / targeted test / runtime smoke oracle이 먼저 정의됐는가
5. 이번 phase가 code-only인지 asset phase인지 명확한가

하나라도 비어 있으면 실행을 미룬다.

## 권장 순서

항상 아래 순서를 기본으로 둔다.

1. code-only
2. compile
3. asset batch
4. validator
5. targeted tests
6. smoke

이 순서를 어기려면 `implement.md`와 `status.md`에 이유를 남긴다.

## Play Mode 금지 작업

Play Mode에서는 아래 작업을 금지한다.

- generator 실행
- reserialize
- batch asset authoring
- 대량 `Execute Menu Item` 기반 content 생성
- validation report를 source-of-truth처럼 저장하는 작업

`SampleSeedGenerator`나 이와 동급의 bootstrap helper는 반드시 Edit Mode에서만 실행한다.

## asset batching 규칙

- asset batch는 code-only compile이 green이 된 뒤에만 시작한다.
- batch edit가 필요하면 `StartAssetEditing / StopAssetEditing`을 `try/finally`로 감싼다.
- batch 중간 실패 시 blind retry를 반복하지 않는다.
- batch 동안 일부 API가 기대대로 동작하지 않을 수 있으므로, 실패하면 authoring transaction 문제로 분류하고 진단으로 전환한다.
- asset phase에서는 compile blocker 수정과 asset generation을 한 micro-loop에 섞지 않는다.

## dirty / save / serialize 주의점

- script가 `ScriptableObject` 내용을 바꿨다면 dirty 처리를 명시한다.
- serialization 중 `SaveAssets`를 호출하지 않는다.
- refresh는 phase 경계에서만 허용하고, 작은 편집마다 습관적으로 호출하지 않는다.
- reserialize나 import 강제 호출은 validator나 runtime smoke보다 앞서지 않는다.

## Execute Menu Item 사용 조건

`Execute Menu Item`은 아래 조건을 만족할 때만 허용한다.

- preflight가 통과했다.
- Edit Mode다.
- batch 범위와 기대 산출물이 task 문서에 적혀 있다.
- 실패 시 되돌릴 범위와 다음 진단 단계가 정해져 있다.

상태 확인 없는 즉시 호출은 금지한다.

## asset authoring failure 시 전환 기준

- 같은 authoring failure가 한 번 반복되면 재시도보다 진단으로 전환한다.
- 진단에는 최소 아래를 남긴다.
  - 실패한 capability 또는 menu action
  - 현재 Editor state
  - 마지막 compile / refresh 시점
  - 어떤 asset family가 partially written 되었는지
- 원인이 Play Mode, refresh timing, serialize guard, asmdef drift 중 어디에 가까운지 분류한다.

## 운영 메모

- `compile green`은 asset phase 진입 gate일 뿐 최종 완료가 아니다.
- validator와 smoke를 asset phase 뒤로 미루더라도, oracle 정의는 task 시작 전에 먼저 있어야 한다.
